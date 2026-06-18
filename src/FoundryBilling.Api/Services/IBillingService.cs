using FoundryBilling.Api.Models;

namespace FoundryBilling.Api.Services;

public interface IBillingService
{
    Task<IReadOnlyList<BillingMetric>> GetBillingMetricsAsync(
        BillingMetricsQuery query,
        CancellationToken cancellationToken = default);

    Task<UsageSummary> GetUsageSummaryAsync(
        UsageSummaryQuery query,
        CancellationToken cancellationToken = default);
}
