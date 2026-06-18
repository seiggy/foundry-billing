using FoundryBilling.Api.Models;

namespace FoundryBilling.Api.Services;

public interface IProjectService
{
    Task<IReadOnlyList<FoundryProject>> GetProjectsAsync(
        FoundryProjectsQuery query,
        CancellationToken cancellationToken = default);

    Task<FoundryProject?> GetProjectAsync(
        FoundryProjectLookup lookup,
        CancellationToken cancellationToken = default);
}
