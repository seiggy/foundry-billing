using FoundryBilling.Api.Data;
using FoundryBilling.Api.Models;
using FoundryBilling.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace FoundryBilling.Api.Endpoints;

public static class AnalyticsEndpoints
{
    private const int TpmBucketMinutes = 60;

    public static RouteGroupBuilder MapAnalyticsEndpoints(this RouteGroupBuilder group)
    {
        var analytics = group.MapGroup("/analytics")
            .WithTags("Analytics");

        analytics.MapGet("/usage", GetUsageAnalyticsAsync)
            .WithName("GetUsageAnalytics")
            .WithSummary("Gets usage analytics with daily, model, and deployment breakdowns.");

        analytics.MapGet("/tpm", GetTpmAnalyticsAsync)
            .WithName("GetTpmAnalytics")
            .WithSummary("Gets per-model TPM analytics for a recent usage window.");

        analytics.MapPost("/ptu-recommendation", GetPtuRecommendationAsync)
            .WithName("GetPtuRecommendation")
            .WithSummary("Calculates PTU sizing and pricing recommendations from synced usage.");

        return group;
    }

    private static async Task<Results<Ok<UsageAnalyticsResponse>, ValidationProblem>> GetUsageAnalyticsAsync(
        int? days,
        BillingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var normalizedDays = days.GetValueOrDefault(30);
        var errors = EndpointValidation.ValidateAnalyticsDays(normalizedDays);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var windowData = await LoadUsageWindowAsync(dbContext, normalizedDays, cancellationToken);
        var dayUsageLookup = windowData.Slices
            .GroupBy(slice => DateOnly.FromDateTime(slice.Timestamp.UtcDateTime))
            .ToDictionary(
                group => group.Key,
                group => new DailyUsagePoint(
                    group.Key,
                    group.Sum(slice => slice.PromptTokens),
                    group.Sum(slice => slice.CompletionTokens),
                    group.Sum(slice => slice.TotalTokens)));

        var dailyUsage = Enumerable.Range(0, windowData.Days)
            .Select(offset => DateOnly.FromDateTime(windowData.WindowStart.UtcDateTime).AddDays(offset))
            .Select(date => dayUsageLookup.TryGetValue(date, out var point)
                ? point
                : new DailyUsagePoint(date, 0, 0, 0))
            .ToList();

        var byModel = windowData.Slices
            .GroupBy(slice => slice.ModelName, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ModelBreakdown(
                group.First().ModelName,
                group.Sum(slice => slice.PromptTokens),
                group.Sum(slice => slice.CompletionTokens),
                group.Sum(slice => slice.TotalTokens),
                group.Select(slice => slice.DeploymentId).Distinct().Count()))
            .OrderByDescending(model => model.TotalTokens)
            .ThenBy(model => model.ModelName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var byDeployment = windowData.Slices
            .GroupBy(slice => slice.DeploymentId)
            .Select(group => new DeploymentBreakdown(
                group.First().DeploymentName,
                group.First().ModelName,
                group.First().HubName,
                group.Sum(slice => slice.PromptTokens),
                group.Sum(slice => slice.CompletionTokens),
                group.Sum(slice => slice.TotalTokens)))
            .OrderByDescending(deployment => deployment.TotalTokens)
            .ThenBy(deployment => deployment.HubName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(deployment => deployment.DeploymentName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var response = new UsageAnalyticsResponse(
            windowData.Days,
            windowData.WindowStart,
            windowData.WindowEnd,
            windowData.Slices.Sum(slice => slice.PromptTokens),
            windowData.Slices.Sum(slice => slice.CompletionTokens),
            windowData.Slices.Sum(slice => slice.TotalTokens),
            dailyUsage,
            byModel,
            byDeployment);

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<TpmAnalyticsResponse>, ValidationProblem>> GetTpmAnalyticsAsync(
        int? days,
        BillingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var normalizedDays = days.GetValueOrDefault(30);
        var errors = EndpointValidation.ValidateAnalyticsDays(normalizedDays);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var windowData = await LoadUsageWindowAsync(dbContext, normalizedDays, cancellationToken);
        var modelInputs = BuildModelTpmInputs(windowData);

        var response = new TpmAnalyticsResponse(
            windowData.Days,
            windowData.TotalMinutesInWindow,
            modelInputs
                .Select(model => new ModelTpmMetrics(
                    model.ModelName,
                    model.TotalTokens,
                    Math.Round(model.AvgTpm, 2, MidpointRounding.AwayFromZero),
                    Math.Round(model.P95Tpm, 2, MidpointRounding.AwayFromZero),
                    Math.Round(model.P99Tpm, 2, MidpointRounding.AwayFromZero),
                    Math.Round(model.MaxTpm, 2, MidpointRounding.AwayFromZero)))
                .ToList());

        return TypedResults.Ok(response);
    }

    private static async Task<Results<Ok<PtuRecommendationResponse>, ValidationProblem>> GetPtuRecommendationAsync(
        PtuCalculationRequest? request,
        BillingDbContext dbContext,
        PtuCalculatorService ptuCalculatorService,
        CancellationToken cancellationToken)
    {
        var calculationRequest = request ?? new PtuCalculationRequest();
        var errors = EndpointValidation.ValidateAnalyticsDays(calculationRequest.Days);

        foreach (var deploymentTypeError in EndpointValidation.ValidateDeploymentType(calculationRequest.DeploymentType))
        {
            errors[deploymentTypeError.Key] = deploymentTypeError.Value;
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var windowData = await LoadUsageWindowAsync(dbContext, calculationRequest.Days, cancellationToken);
        var modelInputs = BuildModelTpmInputs(windowData);
        var response = ptuCalculatorService.CalculateRecommendation(
            modelInputs,
            calculationRequest,
            windowData.TotalMinutesInWindow);

        return TypedResults.Ok(response);
    }

    private static async Task<UsageWindowData> LoadUsageWindowAsync(
        BillingDbContext dbContext,
        int days,
        CancellationToken cancellationToken)
    {
        var windowEnd = DateTimeOffset.UtcNow;
        var windowStartDate = DateOnly.FromDateTime(windowEnd.UtcDateTime).AddDays(-(days - 1));
        var windowStart = new DateTimeOffset(
            DateTime.SpecifyKind(windowStartDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));

        var slices = await dbContext.UsageMetricSlices
            .AsNoTracking()
            .Join(
                dbContext.ModelDeployments.AsNoTracking(),
                slice => slice.DeploymentId,
                deployment => deployment.Id,
                (slice, deployment) => new
                {
                    slice.DeploymentId,
                    slice.Timestamp,
                    slice.IntervalMinutes,
                    slice.PromptTokens,
                    slice.CompletionTokens,
                    slice.TotalTokens,
                    deployment.DeploymentName,
                    deployment.ModelName,
                    deployment.HubId
                })
            .Join(
                dbContext.FoundryHubs.AsNoTracking(),
                slice => slice.HubId,
                hub => hub.Id,
                (slice, hub) => new UsageSliceRow(
                    slice.DeploymentId,
                    slice.DeploymentName,
                    slice.ModelName,
                    hub.Name,
                    slice.Timestamp,
                    slice.IntervalMinutes,
                    slice.PromptTokens,
                    slice.CompletionTokens,
                    slice.TotalTokens))
            .Where(slice => slice.Timestamp >= windowStart && slice.Timestamp <= windowEnd)
            .ToListAsync(cancellationToken);

        return new UsageWindowData(
            days,
            windowStart,
            windowEnd,
            Math.Max(1, (int)Math.Ceiling((windowEnd - windowStart).TotalMinutes)),
            slices);
    }

    private static IReadOnlyList<ModelPtuCalculationInput> BuildModelTpmInputs(UsageWindowData windowData)
    {
        var firstBucket = new DateTimeOffset(
            windowData.WindowStart.UtcDateTime.Year,
            windowData.WindowStart.UtcDateTime.Month,
            windowData.WindowStart.UtcDateTime.Day,
            windowData.WindowStart.UtcDateTime.Hour,
            0,
            0,
            TimeSpan.Zero);
        var bucketCount = Math.Max(
            1,
            (int)Math.Ceiling((windowData.WindowEnd - firstBucket).TotalMinutes / TpmBucketMinutes));

        return windowData.Slices
            .GroupBy(slice => slice.ModelName, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var bucketLookup = group
                    .GroupBy(slice => TruncateToHour(slice.Timestamp))
                    .ToDictionary(
                        bucket => bucket.Key,
                        bucket => bucket.Sum(slice => (double)slice.TotalTokens / Math.Max(1, slice.IntervalMinutes)));

                var bucketTpms = Enumerable.Range(0, bucketCount)
                    .Select(offset =>
                    {
                        var bucketTime = firstBucket.AddHours(offset);
                        return bucketLookup.TryGetValue(bucketTime, out var tpm) ? tpm : 0d;
                    })
                    .ToList();

                var totalTokens = group.Sum(slice => slice.TotalTokens);

                return new ModelPtuCalculationInput(
                    group.First().ModelName,
                    group.Sum(slice => slice.PromptTokens),
                    group.Sum(slice => slice.CompletionTokens),
                    totalTokens,
                    (double)totalTokens / windowData.TotalMinutesInWindow,
                    CalculatePercentile(bucketTpms, 95d),
                    CalculatePercentile(bucketTpms, 99d),
                    bucketTpms.Count == 0 ? 0d : bucketTpms.Max(),
                    bucketTpms,
                    TpmBucketMinutes);
            })
            .OrderByDescending(model => model.TotalTokens)
            .ThenBy(model => model.ModelName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static double CalculatePercentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0d;
        }

        var ordered = values.OrderBy(value => value).ToList();
        var position = (percentile / 100d) * (ordered.Count - 1);
        var lowerIndex = (int)Math.Floor(position);
        var upperIndex = (int)Math.Ceiling(position);

        if (lowerIndex == upperIndex)
        {
            return ordered[lowerIndex];
        }

        var weight = position - lowerIndex;
        return ordered[lowerIndex] + ((ordered[upperIndex] - ordered[lowerIndex]) * weight);
    }

    private static DateTimeOffset TruncateToHour(DateTimeOffset timestamp) =>
        new(
            timestamp.UtcDateTime.Year,
            timestamp.UtcDateTime.Month,
            timestamp.UtcDateTime.Day,
            timestamp.UtcDateTime.Hour,
            0,
            0,
            TimeSpan.Zero);

    private sealed record UsageWindowData(
        int Days,
        DateTimeOffset WindowStart,
        DateTimeOffset WindowEnd,
        int TotalMinutesInWindow,
        IReadOnlyList<UsageSliceRow> Slices);

    private sealed record UsageSliceRow(
        Guid DeploymentId,
        string DeploymentName,
        string ModelName,
        string HubName,
        DateTimeOffset Timestamp,
        int IntervalMinutes,
        long PromptTokens,
        long CompletionTokens,
        long TotalTokens);
}
