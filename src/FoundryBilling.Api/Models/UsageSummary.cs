namespace FoundryBilling.Api.Models;

public sealed record UsageSummary(
    string TenantId,
    string? ProjectId,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string Currency,
    decimal TotalCost,
    int MetricCount,
    int ProjectCount);
