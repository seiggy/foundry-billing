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
            .WithSummary("Gets synced token usage metrics.");

        billing.MapGet("/summary", GetUsageSummaryAsync)
            .WithName("GetUsageSummary")
            .WithSummary("Gets an aggregated synced token usage summary.");

        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<BillingMetricResponse>>, ValidationProblem>> GetBillingMetricsAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        var errors = EndpointValidation.ValidateDateRange(startDate, endDate);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var metrics = await billingService.GetBillingMetricsAsync(startDate, endDate, cancellationToken);

        return TypedResults.Ok(metrics);
    }

    private static async Task<Results<Ok<UsageSummaryResponse>, ValidationProblem>> GetUsageSummaryAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        IBillingService billingService,
        CancellationToken cancellationToken)
    {
        var errors = EndpointValidation.ValidateDateRange(startDate, endDate);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var summary = await billingService.GetUsageSummaryAsync(startDate, endDate, cancellationToken);

        return TypedResults.Ok(summary);
    }
}
