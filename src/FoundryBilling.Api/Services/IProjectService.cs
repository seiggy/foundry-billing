using FoundryBilling.Api.Models;

namespace FoundryBilling.Api.Services;

public interface IProjectService
{
    Task<IReadOnlyList<FoundryProjectResponse>> GetProjectsAsync(
        CancellationToken cancellationToken = default);

    Task<FoundryProjectResponse?> GetProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);
}
