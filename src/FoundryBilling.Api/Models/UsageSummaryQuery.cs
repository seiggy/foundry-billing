namespace FoundryBilling.Api.Models;

public sealed record UsageSummaryQuery(
    string TenantId,
    string? ProjectId,
    DateOnly? StartDate,
    DateOnly? EndDate);
