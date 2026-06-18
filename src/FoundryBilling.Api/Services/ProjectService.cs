using Azure.ResourceManager;
using FoundryBilling.Api.Models;

namespace FoundryBilling.Api.Services;

public sealed class ProjectService : IProjectService
{
    private readonly ArmClient _armClient;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(ArmClient armClient, ILogger<ProjectService> logger)
    {
        _armClient = armClient;
        _logger = logger;
    }

    public Task<IReadOnlyList<FoundryProject>> GetProjectsAsync(
        FoundryProjectsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Returning placeholder Foundry projects for tenant {TenantId} using {ClientType}.",
            query.TenantId,
            _armClient.GetType().Name);

        IReadOnlyList<FoundryProject> projects = Array.Empty<FoundryProject>();
        return Task.FromResult(projects);
    }

    public async Task<FoundryProject?> GetProjectAsync(
        FoundryProjectLookup lookup,
        CancellationToken cancellationToken = default)
    {
        var projects = await GetProjectsAsync(new FoundryProjectsQuery(lookup.TenantId), cancellationToken);

        return projects.FirstOrDefault(project =>
            string.Equals(project.ProjectId, lookup.ProjectId, StringComparison.OrdinalIgnoreCase));
    }
}
