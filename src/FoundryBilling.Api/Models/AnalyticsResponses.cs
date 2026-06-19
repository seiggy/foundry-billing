namespace FoundryBilling.Api.Models;

public sealed record UsageAnalyticsResponse(
    int Days,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    long TotalPromptTokens,
    long TotalCompletionTokens,
    long TotalTokens,
    IReadOnlyList<DailyUsagePoint> DailyUsage,
    IReadOnlyList<ModelBreakdown> ByModel,
    IReadOnlyList<DeploymentBreakdown> ByDeployment);

public sealed record DailyUsagePoint(
    DateOnly Date,
    long PromptTokens,
    long CompletionTokens,
    long TotalTokens);

public sealed record ModelBreakdown(
    string ModelName,
    long PromptTokens,
    long CompletionTokens,
    long TotalTokens,
    int DeploymentCount);

public sealed record DeploymentBreakdown(
    string DeploymentName,
    string ModelName,
    string HubName,
    long PromptTokens,
    long CompletionTokens,
    long TotalTokens);

public sealed record TpmAnalyticsResponse(
    int Days,
    int TotalMinutesInWindow,
    IReadOnlyList<ModelTpmMetrics> Models);

public sealed record ModelTpmMetrics(
    string ModelName,
    long TotalTokens,
    double AvgTpm,
    double P95Tpm,
    double P99Tpm,
    double MaxTpm);

public sealed record PtuCalculationRequest(
    int Days = 30,
    Dictionary<string, decimal>? CustomInputRates = null,
    Dictionary<string, decimal>? CustomOutputRates = null,
    Dictionary<string, int>? CustomTpmPerPtu = null,
    string DeploymentType = "Global");

public sealed record PtuRecommendationResponse(
    IReadOnlyList<ModelPtuRecommendation> Models,
    PtuCostComparison CostComparison);

public sealed record ModelPtuRecommendation(
    string ModelName,
    double AvgTpm,
    double P99Tpm,
    int TpmPerPtu,
    int RecommendedPtus,
    int MinimumPtus,
    double UtilizationAtRecommended);

public sealed record PtuCostComparison(
    decimal PaygoCostEstimate,
    decimal PtuOnDemandMonthly,
    decimal PtuMonthlyReserved,
    decimal PtuYearlyReserved,
    decimal SpilloverEstimate,
    string Recommendation);

internal sealed record ModelPtuCalculationInput(
    string ModelName,
    long PromptTokens,
    long CompletionTokens,
    long TotalTokens,
    double AvgTpm,
    double P95Tpm,
    double P99Tpm,
    double MaxTpm,
    IReadOnlyList<double> BucketTpms,
    int BucketDurationMinutes);
