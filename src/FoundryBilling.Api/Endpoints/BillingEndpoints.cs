using FoundryBilling.Api.Models;
using FoundryBilling.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FoundryBilling.Api.Endpoints;

public static class BillingEndpoints
{
    public static RouteGroupBuilder MapBillingEndpoints(this RouteGroupBuilder group)
    {
        var billing = group.MapGroup("/billing")
            .WithTags("Billing");

        billing.MapGet("/metrics", GetBillingMetricsAsync)
            .WithName("GetBillingMetrics")
            .WithSummary("Gets billing metrics for a tenant or project.");

        billing.MapGet("/summary", GetUsageSummaryAsync)
            .WithName("GetUsageSummary")
            .WithSummary("Gets an aggregated billing summary for a tenant or project.");

        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<BillingMetric>>, ValidationProblem>> GetBillingMetricsAsync(
        string tenantId,
        string? projectId,
        DateOnly? startDate,
        DateOnly? endDate,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        var errors = EndpointValidation.ValidateTenantAndDateRange(tenantId, startDate, endDate);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var query = new BillingMetricsQuery(tenantId, projectId, startDate, endDate);
        var metrics = await billingService.GetBillingMetricsAsync(query, cancellationToken);

        return TypedResults.Ok(metrics);
    }

    private static async Task<Results<Ok<UsageSummary>, ValidationProblem>> GetUsageSummaryAsync(
        string tenantId,
        string? projectId,
        DateOnly? startDate,
        DateOnly? endDate,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        var errors = EndpointValidation.ValidateTenantAndDateRange(tenantId, startDate, endDate);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var query = new UsageSummaryQuery(tenantId, projectId, startDate, endDate);
        var summary = await billingService.GetUsageSummaryAsync(query, cancellationToken);

        return TypedResults.Ok(summary);
    }
}
