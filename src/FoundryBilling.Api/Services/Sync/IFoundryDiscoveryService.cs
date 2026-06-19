using FoundryBilling.Api.Models.Sync;

namespace FoundryBilling.Api.Services.Sync;

public interface IFoundryDiscoveryService
{
    Task<IReadOnlyList<DiscoveredHub>> DiscoverHubsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<DiscoveredProject>> DiscoverProjectsAsync(string hubResourceId, CancellationToken ct = default);

    Task<IReadOnlyList<DiscoveredDeployment>> DiscoverDeploymentsAsync(string hubResourceId, CancellationToken ct = default);
}
