using Azure.ResourceManager;
using FoundryBilling.Api.Models;

namespace FoundryBilling.Api.Services;

public sealed class BillingService : IBillingService
{
    private readonly ArmClient _armClient;
    private readonly IProjectService _projectService;
    private readonly ILogger<BillingService> _logger;

    public BillingService(ArmClient armClient, IProjectService projectService, ILogger<BillingService> logger)
    {
        _armClient = armClient;
        _projectService = projectService;
        _logger = logger;
    }

    public Task<IReadOnlyList<BillingMetric>> GetBillingMetricsAsync(
        BillingMetricsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Returning placeholder billing metrics for tenant {TenantId} and project {ProjectId} using {ClientType}.",
            query.TenantId,
            query.ProjectId,
            _armClient.GetType().Name);

        IReadOnlyList<BillingMetric> metrics = Array.Empty<BillingMetric>();
        return Task.FromResult(metrics);
    }

    public async Task<UsageSummary> GetUsageSummaryAsync(
        UsageSummaryQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Returning placeholder usage summary for tenant {TenantId} and project {ProjectId} using {ClientType}.",
            query.TenantId,
            query.ProjectId,
            _armClient.GetType().Name);

        var projectCount = await GetProjectCountAsync(query, cancellationToken);

        return new UsageSummary(
            query.TenantId,
            query.ProjectId,
            query.StartDate,
            query.EndDate,
            "USD",
            0m,
            0,
            projectCount);
    }

    private async Task<int> GetProjectCountAsync(UsageSummaryQuery query, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.ProjectId))
        {
            var project = await _projectService.GetProjectAsync(
                new FoundryProjectLookup(query.TenantId, query.ProjectId),
                cancellationToken);

            return project is null ? 0 : 1;
        }

        var projects = await _projectService.GetProjectsAsync(new FoundryProjectsQuery(query.TenantId), cancellationToken);
        return projects.Count;
    }
}
