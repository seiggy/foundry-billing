using FoundryBilling.Api.Models;

namespace FoundryBilling.Api.Services;

public sealed class PtuCalculatorService
{
    private const decimal DefaultInputRatePerMillion = 2.50m;
    private const decimal DefaultOutputRatePerMillion = 10.00m;
    private const int DefaultTpmPerPtu = 2_500;
    private const decimal MonthlyHours = 24m * 30m;

    private static readonly IReadOnlyDictionary<string, int> DefaultTpmPerPtuByModel =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-4o"] = 2_500,
            ["gpt-4o-mini"] = 10_000,
            ["gpt-4.1"] = 3_000,
            ["gpt-4.1-mini"] = 10_000,
            ["gpt-4.1-nano"] = 20_000,
            ["o3-mini"] = 1_000,
            ["o3"] = 500,
            ["o4-mini"] = 5_000
        };

    private static readonly IReadOnlyDictionary<string, ModelPaygoRate> DefaultPaygoRatesByModel =
        new Dictionary<string, ModelPaygoRate>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-4o"] = new(2.50m, 10.00m),
            ["gpt-4o-mini"] = new(0.15m, 0.60m),
            ["gpt-4.1"] = new(2.00m, 8.00m),
            ["gpt-4.1-mini"] = new(0.40m, 1.60m),
            ["gpt-4.1-nano"] = new(0.10m, 0.40m),
            ["o3-mini"] = new(1.10m, 4.40m),
            ["o3"] = new(2.00m, 8.00m),
            ["o4-mini"] = new(1.10m, 4.40m)
        };

    private static readonly IReadOnlyDictionary<string, DeploymentPricingProfile> PricingProfiles =
        new Dictionary<string, DeploymentPricingProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["global"] = new(1.00m, 2.00m, 0.72m, 0.60m),
            ["datazone"] = new(1.10m, 2.20m, 0.79m, 0.66m),
            ["regional"] = new(1.20m, 2.40m, 0.86m, 0.72m)
        };

    internal PtuRecommendationResponse CalculateRecommendation(
        IReadOnlyList<ModelPtuCalculationInput> modelInputs,
        PtuCalculationRequest request,
        int totalMinutesInWindow)
    {
        var pricingProfile = GetPricingProfile(request.DeploymentType);
        var monthlyScale = totalMinutesInWindow > 0
            ? MonthlyHours * 60m / totalMinutesInWindow
            : 0m;

        var calculatedModels = modelInputs
            .Select(input => BuildModelCalculation(input, request, pricingProfile))
            .OrderByDescending(model => model.Recommendation.RecommendedPtus)
            .ThenByDescending(model => model.Recommendation.P99Tpm)
            .ThenBy(model => model.Recommendation.ModelName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var paygoCostEstimate = decimal.Round(
            calculatedModels.Sum(model => model.PaygoCostEstimate),
            2,
            MidpointRounding.AwayFromZero);

        var paygoMonthlyEquivalent = decimal.Round(
            paygoCostEstimate * monthlyScale,
            2,
            MidpointRounding.AwayFromZero);

        var totalRecommendedPtus = calculatedModels.Sum(model => model.Recommendation.RecommendedPtus);
        var totalMinimumPtus = calculatedModels.Sum(model => model.Recommendation.MinimumPtus);
        var ptuOnDemandMonthly = decimal.Round(
            totalRecommendedPtus * pricingProfile.OnDemandPtuHourlyRate * MonthlyHours,
            2,
            MidpointRounding.AwayFromZero);
        var ptuMonthlyReserved = decimal.Round(
            totalRecommendedPtus * pricingProfile.MonthlyReservedPtuHourlyRate * MonthlyHours,
            2,
            MidpointRounding.AwayFromZero);
        var ptuYearlyReserved = decimal.Round(
            totalRecommendedPtus * pricingProfile.YearlyReservedPtuHourlyRate * MonthlyHours,
            2,
            MidpointRounding.AwayFromZero);

        var burstPaygoEstimate = decimal.Round(
            calculatedModels.Sum(model => model.BurstPaygoEstimate) * monthlyScale,
            2,
            MidpointRounding.AwayFromZero);
        var spilloverEstimate = decimal.Round(
            totalMinimumPtus * pricingProfile.MonthlyReservedPtuHourlyRate * MonthlyHours + burstPaygoEstimate,
            2,
            MidpointRounding.AwayFromZero);

        var totalAvgTpm = calculatedModels.Sum(model => model.Recommendation.AvgTpm);
        var totalRecommendedCapacity = calculatedModels.Sum(model => model.Recommendation.RecommendedPtus * (double)model.Recommendation.TpmPerPtu);
        var utilization = totalRecommendedCapacity > 0
            ? totalAvgTpm / totalRecommendedCapacity
            : 0d;

        var recommendation = DetermineRecommendation(
            paygoMonthlyEquivalent,
            ptuMonthlyReserved,
            ptuYearlyReserved,
            spilloverEstimate,
            utilization);

        return new PtuRecommendationResponse(
            calculatedModels.Select(model => model.Recommendation).ToList(),
            new PtuCostComparison(
                paygoCostEstimate,
                ptuOnDemandMonthly,
                ptuMonthlyReserved,
                ptuYearlyReserved,
                spilloverEstimate,
                recommendation));
    }

    private static CalculatedModelPtu BuildModelCalculation(
        ModelPtuCalculationInput input,
        PtuCalculationRequest request,
        DeploymentPricingProfile pricingProfile)
    {
        var tpmPerPtu = ResolveTpmPerPtu(input.ModelName, request.CustomTpmPerPtu);
        var recommendedPtus = input.TotalTokens > 0
            ? Math.Max(1, (int)Math.Ceiling(input.P99Tpm / tpmPerPtu))
            : 0;
        var minimumPtus = input.TotalTokens > 0
            ? Math.Max(1, (int)Math.Ceiling(input.AvgTpm / tpmPerPtu))
            : 0;
        var utilizationAtRecommended = recommendedPtus > 0
            ? Math.Clamp(input.AvgTpm / (recommendedPtus * (double)tpmPerPtu), 0d, 1d)
            : 0d;

        var rates = ResolveRates(
            input.ModelName,
            pricingProfile,
            request.CustomInputRates,
            request.CustomOutputRates);

        var paygoCostEstimate = EstimatePaygoCost(
            input.PromptTokens,
            input.CompletionTokens,
            rates.InputRatePerMillion,
            rates.OutputRatePerMillion);

        var baseCapacityTpm = minimumPtus * (double)tpmPerPtu;
        var burstTokens = input.BucketTpms.Sum(bucketTpm =>
            Math.Max(0d, bucketTpm - baseCapacityTpm) * input.BucketDurationMinutes);

        var promptShare = input.TotalTokens > 0
            ? (decimal)input.PromptTokens / input.TotalTokens
            : 0m;
        var burstPromptTokens = (long)Math.Round(
            burstTokens * (double)promptShare,
            MidpointRounding.AwayFromZero);
        var burstCompletionTokens = (long)Math.Round(
            Math.Max(0d, burstTokens - burstPromptTokens),
            MidpointRounding.AwayFromZero);

        var burstPaygoEstimate = EstimatePaygoCost(
            burstPromptTokens,
            burstCompletionTokens,
            rates.InputRatePerMillion,
            rates.OutputRatePerMillion);

        return new CalculatedModelPtu(
            new ModelPtuRecommendation(
                input.ModelName,
                Math.Round(input.AvgTpm, 2, MidpointRounding.AwayFromZero),
                Math.Round(input.P99Tpm, 2, MidpointRounding.AwayFromZero),
                tpmPerPtu,
                recommendedPtus,
                minimumPtus,
                Math.Round(utilizationAtRecommended, 4, MidpointRounding.AwayFromZero)),
            paygoCostEstimate,
            burstPaygoEstimate);
    }

    private static int ResolveTpmPerPtu(
        string modelName,
        IReadOnlyDictionary<string, int>? customTpmPerPtu)
    {
        if (TryResolveOverride(modelName, customTpmPerPtu, out var overrideValue) && overrideValue > 0)
        {
            return overrideValue;
        }

        if (TryResolveKnownModel(modelName, DefaultTpmPerPtuByModel, out var knownValue) && knownValue > 0)
        {
            return knownValue;
        }

        return DefaultTpmPerPtu;
    }

    private static ModelPaygoRate ResolveRates(
        string modelName,
        DeploymentPricingProfile pricingProfile,
        IReadOnlyDictionary<string, decimal>? customInputRates,
        IReadOnlyDictionary<string, decimal>? customOutputRates)
    {
        var defaultRates = TryResolveKnownModel(modelName, DefaultPaygoRatesByModel, out var knownRates)
            ? knownRates
            : new ModelPaygoRate(DefaultInputRatePerMillion, DefaultOutputRatePerMillion);

        var inputRate = TryResolveOverride(modelName, customInputRates, out var customInputRate)
            ? customInputRate
            : defaultRates.InputRatePerMillion * pricingProfile.PaygoRateMultiplier;

        var outputRate = TryResolveOverride(modelName, customOutputRates, out var customOutputRate)
            ? customOutputRate
            : defaultRates.OutputRatePerMillion * pricingProfile.PaygoRateMultiplier;

        return new ModelPaygoRate(inputRate, outputRate);
    }

    private static string DetermineRecommendation(
        decimal paygoMonthlyEquivalent,
        decimal ptuMonthlyReserved,
        decimal ptuYearlyReserved,
        decimal spilloverEstimate,
        double utilization)
    {
        if (paygoMonthlyEquivalent <= 0m)
        {
            return "PAYGO";
        }

        if (paygoMonthlyEquivalent < ptuMonthlyReserved)
        {
            return "PAYGO";
        }

        if (utilization > 0.70d && ptuYearlyReserved < paygoMonthlyEquivalent)
        {
            return "PTU_YEARLY";
        }

        if (utilization > 0.50d)
        {
            if (spilloverEstimate < ptuMonthlyReserved && spilloverEstimate < paygoMonthlyEquivalent)
            {
                return "SPILLOVER";
            }

            return "PTU_MONTHLY";
        }

        return "PAYGO";
    }

    private static decimal EstimatePaygoCost(
        long promptTokens,
        long completionTokens,
        decimal inputRatePerMillion,
        decimal outputRatePerMillion)
    {
        var promptCost = promptTokens / 1_000_000m * inputRatePerMillion;
        var completionCost = completionTokens / 1_000_000m * outputRatePerMillion;

        return decimal.Round(promptCost + completionCost, 2, MidpointRounding.AwayFromZero);
    }

    private static DeploymentPricingProfile GetPricingProfile(string? deploymentType)
    {
        if (!string.IsNullOrWhiteSpace(deploymentType)
            && PricingProfiles.TryGetValue(deploymentType.Trim(), out var profile))
        {
            return profile;
        }

        return PricingProfiles["global"];
    }

    private static bool TryResolveOverride<TValue>(
        string modelName,
        IReadOnlyDictionary<string, TValue>? overrides,
        out TValue value)
    {
        if (overrides is not null && TryResolveKnownModel(modelName, overrides, out value))
        {
            return true;
        }

        value = default!;
        return false;
    }

    private static bool TryResolveKnownModel<TValue>(
        string modelName,
        IReadOnlyDictionary<string, TValue> values,
        out TValue value)
    {
        var normalizedModelName = NormalizeModelKey(modelName);

        foreach (var candidate in values.Keys
                     .Select(key => new { Key = key, NormalizedKey = NormalizeModelKey(key) })
                     .OrderByDescending(candidate => candidate.NormalizedKey.Length))
        {
            if (string.Equals(normalizedModelName, candidate.NormalizedKey, StringComparison.OrdinalIgnoreCase)
                || normalizedModelName.StartsWith(candidate.NormalizedKey, StringComparison.OrdinalIgnoreCase))
            {
                value = values[candidate.Key];
                return true;
            }
        }

        value = default!;
        return false;
    }

    private static string NormalizeModelKey(string modelName) =>
        modelName.Trim().Replace('_', '-').ToLowerInvariant();

    private sealed record ModelPaygoRate(
        decimal InputRatePerMillion,
        decimal OutputRatePerMillion);

    private sealed record DeploymentPricingProfile(
        decimal PaygoRateMultiplier,
        decimal OnDemandPtuHourlyRate,
        decimal MonthlyReservedPtuHourlyRate,
        decimal YearlyReservedPtuHourlyRate);

    private sealed record CalculatedModelPtu(
        ModelPtuRecommendation Recommendation,
        decimal PaygoCostEstimate,
        decimal BurstPaygoEstimate);
}
