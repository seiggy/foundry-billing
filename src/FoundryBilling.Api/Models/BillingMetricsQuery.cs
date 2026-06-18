namespace FoundryBilling.Api.Models;

public sealed record BillingMetricsQuery(
    string TenantId,
    string? ProjectId,
    DateOnly? StartDate,
    DateOnly? EndDate);
