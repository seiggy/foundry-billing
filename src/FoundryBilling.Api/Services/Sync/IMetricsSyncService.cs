using FoundryBilling.Api.Models.Sync;

namespace FoundryBilling.Api.Services.Sync;

public interface IMetricsSyncService
{
    Task<IReadOnlyList<MetricDataPoint>> GetTokenUsageAsync(
        string resourceId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default);
}
