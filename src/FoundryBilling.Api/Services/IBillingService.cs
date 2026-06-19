using FoundryBilling.Api.Models;

namespace FoundryBilling.Api.Services;

public interface IBillingService
{
    Task<IReadOnlyList<BillingMetricResponse>> GetBillingMetricsAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default);

    Task<UsageSummaryResponse> GetUsageSummaryAsync(
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken = default);
}
