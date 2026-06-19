using FoundryBilling.Api.Data;
using FoundryBilling.Api.Data.Entities;
using FoundryBilling.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FoundryBilling.Api.Services;

public sealed class BillingService(BillingDbContext dbContext, ILogger<BillingService> logger) : IBillingService
{
    public async Task<IReadOnlyList<BillingMetricResponse>> GetBillingMetricsAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Querying usage metric slices for billing metrics with start date {StartDate} and end date {EndDate}.",
            startDate,
            endDate);

        return await ApplyDateRange(dbContext.UsageMetricSlices.AsNoTracking(), startDate, endDate)
            .OrderBy(metric => metric.Timestamp)
            .ThenBy(metric => metric.Deployment.Hub.Name)
            .ThenBy(metric => metric.Deployment.DeploymentName)
            .Select(metric => new BillingMetricResponse(
                metric.Deployment.DeploymentName,
                metric.Deployment.ModelName,
                metric.Deployment.ModelVersion,
                metric.Deployment.Hub.Name,
                metric.Timestamp,
                metric.PromptTokens,
                metric.CompletionTokens,
                metric.TotalTokens))
            .ToListAsync(cancellationToken);
    }

    public async Task<UsageSummaryResponse> GetUsageSummaryAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Querying usage summary with start date {StartDate} and end date {EndDate}.",
            startDate,
            endDate);

        var hubCount = await dbContext.FoundryHubs.AsNoTracking().CountAsync(cancellationToken);
        var projectCount = await dbContext.FoundryProjects.AsNoTracking().CountAsync(cancellationToken);
        var deploymentCount = await dbContext.ModelDeployments.AsNoTracking().CountAsync(cancellationToken);

        var metrics = ApplyDateRange(dbContext.UsageMetricSlices.AsNoTracking(), startDate, endDate);

        var aggregate = await metrics
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalPromptTokens = group.Sum(metric => metric.PromptTokens),
                TotalCompletionTokens = group.Sum(metric => metric.CompletionTokens),
                TotalTokens = group.Sum(metric => metric.TotalTokens),
                OldestMetric = (DateTimeOffset?)group.Min(metric => metric.Timestamp),
                NewestMetric = (DateTimeOffset?)group.Max(metric => metric.Timestamp)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var sliceData = await dbContext.UsageMetricSlices
            .AsNoTracking()
            .Join(dbContext.ModelDeployments,
                slice => slice.DeploymentId,
                deployment => deployment.Id,
                (slice, deployment) => new
                {
                    deployment.ModelName,
                    slice.PromptTokens,
                    slice.CompletionTokens,
                    slice.TotalTokens,
                    slice.Timestamp
                })
            .Where(x => startDate == null || x.Timestamp >= new DateTimeOffset(startDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero))
            .Where(x => endDate == null || x.Timestamp < new DateTimeOffset(endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero))
            .ToListAsync(cancellationToken);

        var byModel = sliceData
            .GroupBy(x => x.ModelName)
            .Select(group => new ModelUsageBreakdown(
                group.Key,
                group.Sum(x => x.PromptTokens),
                group.Sum(x => x.CompletionTokens),
                group.Sum(x => x.TotalTokens)))
            .OrderByDescending(x => x.TotalTokens)
            .ThenBy(x => x.ModelName)
            .ToList();

        return new UsageSummaryResponse(
            hubCount,
            projectCount,
            deploymentCount,
            aggregate?.TotalPromptTokens ?? 0,
            aggregate?.TotalCompletionTokens ?? 0,
            aggregate?.TotalTokens ?? 0,
            aggregate?.OldestMetric,
            aggregate?.NewestMetric,
            byModel);
    }

    private static IQueryable<UsageMetricSlice> ApplyDateRange(
        IQueryable<UsageMetricSlice> query,
        DateOnly? startDate,
        DateOnly? endDate)
    {
        if (startDate.HasValue)
        {
            var startInclusive = new DateTimeOffset(
                DateTime.SpecifyKind(startDate.Value.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));
            query = query.Where(metric => metric.Timestamp >= startInclusive);
        }

        if (endDate.HasValue)
        {
            var endExclusive = new DateTimeOffset(
                DateTime.SpecifyKind(endDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc));
            query = query.Where(metric => metric.Timestamp < endExclusive);
        }

        return query;
    }
}
