using FoundryBilling.Api.Data;
using FoundryBilling.Api.Data.Entities;
using FoundryBilling.Api.Infrastructure;
using FoundryBilling.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FoundryBilling.Api.Workers;

public sealed class MetricsSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptionsMonitor<SyncOptions> _syncOptions;
    private readonly ILogger<MetricsSyncWorker> _logger;

    public MetricsSyncWorker(
        IServiceScopeFactory serviceScopeFactory,
        IOptionsMonitor<SyncOptions> syncOptions,
        ILogger<MetricsSyncWorker> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _syncOptions = syncOptions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metrics sync worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleSafelyAsync(stoppingToken);

            var interval = GetInterval();
            using var timer = new PeriodicTimer(interval);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Metrics sync worker stopped.");
    }

    private async Task RunCycleSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RunCycleAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metrics sync cycle failed.");
        }
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var discoveryService = scope.ServiceProvider.GetRequiredService<IFoundryDiscoveryService>();
        var metricsSyncService = scope.ServiceProvider.GetRequiredService<IMetricsSyncService>();
        var syncOptions = _syncOptions.CurrentValue;
        var syncTimestamp = DateTimeOffset.UtcNow;

        var discoveredHubs = await discoveryService.DiscoverHubsAsync(stoppingToken);
        var existingHubs = await dbContext.FoundryHubs.ToListAsync(stoppingToken);
        var hubsByResourceId = existingHubs.ToDictionary(hub => hub.AzureResourceId, StringComparer.OrdinalIgnoreCase);

        var summary = new SyncSummary();
        foreach (var discoveredHub in discoveredHubs)
        {
            if (!hubsByResourceId.TryGetValue(discoveredHub.ResourceId, out var hubEntity))
            {
                hubEntity = new FoundryHub
                {
                    Id = Guid.NewGuid(),
                    AzureResourceId = discoveredHub.ResourceId,
                    Name = discoveredHub.Name,
                    SubscriptionId = discoveredHub.SubscriptionId,
                    ResourceGroup = discoveredHub.ResourceGroup,
                    Region = discoveredHub.Region,
                    LastSyncedAt = syncTimestamp
                };

                dbContext.FoundryHubs.Add(hubEntity);
                hubsByResourceId[discoveredHub.ResourceId] = hubEntity;
                summary.HubsInserted++;
            }
            else
            {
                hubEntity.Name = discoveredHub.Name;
                hubEntity.SubscriptionId = discoveredHub.SubscriptionId;
                hubEntity.ResourceGroup = discoveredHub.ResourceGroup;
                hubEntity.Region = discoveredHub.Region;
                hubEntity.LastSyncedAt = syncTimestamp;
                summary.HubsUpdated++;
            }
        }

        await dbContext.SaveChangesAsync(stoppingToken);

        foreach (var discoveredHub in discoveredHubs)
        {
            if (!hubsByResourceId.TryGetValue(discoveredHub.ResourceId, out var hubEntity))
            {
                continue;
            }

            try
            {
                await SyncHubAsync(
                    dbContext,
                    discoveryService,
                    metricsSyncService,
                    hubEntity,
                    syncOptions,
                    syncTimestamp,
                    summary,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Metrics sync failed for hub {HubName} ({HubResourceId}).", hubEntity.Name, hubEntity.AzureResourceId);
            }
        }

        _logger.LogInformation(
            "Metrics sync cycle completed. Hubs: {HubsInserted} inserted, {HubsUpdated} updated. Projects: {ProjectsInserted} inserted, {ProjectsUpdated} updated. Deployments: {DeploymentsInserted} inserted, {DeploymentsUpdated} updated. Usage slices inserted: {UsageSlicesInserted}.",
            summary.HubsInserted,
            summary.HubsUpdated,
            summary.ProjectsInserted,
            summary.ProjectsUpdated,
            summary.DeploymentsInserted,
            summary.DeploymentsUpdated,
            summary.UsageSlicesInserted);
    }

    private async Task SyncHubAsync(
        BillingDbContext dbContext,
        IFoundryDiscoveryService discoveryService,
        IMetricsSyncService metricsSyncService,
        FoundryHub hubEntity,
        SyncOptions syncOptions,
        DateTimeOffset syncTimestamp,
        SyncSummary summary,
        CancellationToken stoppingToken)
    {
        var existingProjects = await dbContext.FoundryProjects
            .Where(project => project.HubId == hubEntity.Id)
            .ToListAsync(stoppingToken);
        var projectsByResourceId = existingProjects.ToDictionary(project => project.AzureResourceId, StringComparer.OrdinalIgnoreCase);

        var discoveredProjects = await discoveryService.DiscoverProjectsAsync(hubEntity.AzureResourceId, stoppingToken);
        foreach (var discoveredProject in discoveredProjects)
        {
            var projectName = string.IsNullOrWhiteSpace(discoveredProject.DisplayName)
                ? discoveredProject.Name
                : discoveredProject.DisplayName;

            if (!projectsByResourceId.TryGetValue(discoveredProject.ResourceId, out var projectEntity))
            {
                dbContext.FoundryProjects.Add(new FoundryProject
                {
                    Id = Guid.NewGuid(),
                    HubId = hubEntity.Id,
                    AzureResourceId = discoveredProject.ResourceId,
                    Name = projectName,
                    LastSyncedAt = syncTimestamp
                });

                summary.ProjectsInserted++;
                continue;
            }

            projectEntity.Name = projectName;
            projectEntity.LastSyncedAt = syncTimestamp;
            summary.ProjectsUpdated++;
        }

        var existingDeployments = await dbContext.ModelDeployments
            .Where(deployment => deployment.HubId == hubEntity.Id)
            .ToListAsync(stoppingToken);
        var deploymentsByResourceId = existingDeployments.ToDictionary(deployment => deployment.AzureResourceId, StringComparer.OrdinalIgnoreCase);

        var discoveredDeployments = await discoveryService.DiscoverDeploymentsAsync(hubEntity.AzureResourceId, stoppingToken);
        foreach (var discoveredDeployment in discoveredDeployments)
        {
            if (!deploymentsByResourceId.TryGetValue(discoveredDeployment.ResourceId, out var deploymentEntity))
            {
                dbContext.ModelDeployments.Add(new ModelDeployment
                {
                    Id = Guid.NewGuid(),
                    HubId = hubEntity.Id,
                    AzureResourceId = discoveredDeployment.ResourceId,
                    DeploymentName = discoveredDeployment.DeploymentName,
                    ModelName = discoveredDeployment.ModelName,
                    ModelVersion = discoveredDeployment.ModelVersion,
                    SkuName = discoveredDeployment.SkuName,
                    Capacity = discoveredDeployment.Capacity,
                    LastSyncedAt = syncTimestamp
                });

                summary.DeploymentsInserted++;
                continue;
            }

            deploymentEntity.DeploymentName = discoveredDeployment.DeploymentName;
            deploymentEntity.ModelName = discoveredDeployment.ModelName;
            deploymentEntity.ModelVersion = discoveredDeployment.ModelVersion;
            deploymentEntity.SkuName = discoveredDeployment.SkuName;
            deploymentEntity.Capacity = discoveredDeployment.Capacity;
            deploymentEntity.LastSyncedAt = syncTimestamp;
            summary.DeploymentsUpdated++;
        }

        await dbContext.SaveChangesAsync(stoppingToken);

        var metricsWindowEnd = syncTimestamp;
        var metricsWindowStart = metricsWindowEnd.AddHours(-Math.Max(1, syncOptions.MetricLookbackHours));
        var metricDataPoints = await metricsSyncService.GetTokenUsageAsync(
            hubEntity.AzureResourceId,
            metricsWindowStart,
            metricsWindowEnd,
            stoppingToken);

        var deploymentLookup = await dbContext.ModelDeployments
            .Where(deployment => deployment.HubId == hubEntity.Id)
            .ToDictionaryAsync(deployment => deployment.DeploymentName, StringComparer.OrdinalIgnoreCase, stoppingToken);

        var deploymentIds = deploymentLookup.Values.Select(deployment => deployment.Id).ToArray();
        var existingSliceKeys = await dbContext.UsageMetricSlices
            .Where(slice =>
                deploymentIds.Contains(slice.DeploymentId) &&
                slice.Timestamp >= metricsWindowStart &&
                slice.Timestamp <= metricsWindowEnd &&
                slice.IntervalMinutes == 60)
            .Select(slice => new SliceKey(slice.DeploymentId, slice.Timestamp))
            .ToListAsync(stoppingToken);

        var knownSliceKeys = existingSliceKeys.ToHashSet();

        foreach (var metricDataPoint in metricDataPoints)
        {
            if (!deploymentLookup.TryGetValue(metricDataPoint.DeploymentName, out var deploymentEntity))
            {
                _logger.LogWarning(
                    "Skipping metrics for deployment {DeploymentName} because it was not found in the database for hub {HubName}.",
                    metricDataPoint.DeploymentName,
                    hubEntity.Name);
                continue;
            }

            var sliceKey = new SliceKey(deploymentEntity.Id, metricDataPoint.Timestamp);
            if (!knownSliceKeys.Add(sliceKey))
            {
                continue;
            }

            dbContext.UsageMetricSlices.Add(new UsageMetricSlice
            {
                Id = Guid.NewGuid(),
                DeploymentId = deploymentEntity.Id,
                Timestamp = metricDataPoint.Timestamp,
                IntervalMinutes = 60,
                PromptTokens = metricDataPoint.PromptTokens,
                CompletionTokens = metricDataPoint.CompletionTokens,
                TotalTokens = metricDataPoint.TotalTokens
            });

            summary.UsageSlicesInserted++;
        }

        await dbContext.SaveChangesAsync(stoppingToken);
    }

    private TimeSpan GetInterval()
        => TimeSpan.FromMinutes(Math.Max(1, _syncOptions.CurrentValue.IntervalMinutes));

    private sealed class SyncSummary
    {
        public int HubsInserted { get; set; }

        public int HubsUpdated { get; set; }

        public int ProjectsInserted { get; set; }

        public int ProjectsUpdated { get; set; }

        public int DeploymentsInserted { get; set; }

        public int DeploymentsUpdated { get; set; }

        public int UsageSlicesInserted { get; set; }
    }

    private sealed record SliceKey(Guid DeploymentId, DateTimeOffset Timestamp);
}
