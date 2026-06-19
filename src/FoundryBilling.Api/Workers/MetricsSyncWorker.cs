using FoundryBilling.Api.Data;
using FoundryBilling.Api.Data.Entities;
using FoundryBilling.Api.Infrastructure;
using FoundryBilling.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

namespace FoundryBilling.Api.Workers;

public sealed class MetricsSyncWorker : BackgroundService, ISyncTriggerService
{
    private const string RunningStatus = "Running";
    private const string CompletedStatus = "Completed";
    private const string FailedStatus = "Failed";

    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IOptionsMonitor<SyncOptions> _syncOptions;
    private readonly ILogger<MetricsSyncWorker> _logger;
    private readonly object _stateLock = new();
    private readonly Channel<bool> _triggerChannel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });

    private volatile bool _isRunning;
    private SyncRunStatus? _currentRun;
    private SyncRunStatus? _pendingRun;

    public MetricsSyncWorker(
        IServiceScopeFactory serviceScopeFactory,
        IOptionsMonitor<SyncOptions> syncOptions,
        ILogger<MetricsSyncWorker> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _syncOptions = syncOptions;
        _logger = logger;
    }

    public bool IsRunning => _isRunning;

    public SyncRunStatus? CurrentRun
    {
        get
        {
            lock (_stateLock)
            {
                return _currentRun;
            }
        }
    }

    public SyncRunStatus? PendingRun
    {
        get
        {
            lock (_stateLock)
            {
                return _pendingRun;
            }
        }
    }

    public Task TriggerSyncAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        lock (_stateLock)
        {
            _pendingRun ??= new SyncRunStatus(Guid.NewGuid(), DateTimeOffset.UtcNow, RunningStatus);
        }

        _triggerChannel.Writer.TryWrite(true);
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Metrics sync worker started.");

        await RunCycleSafelyAsync(null, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var pendingRun = TryDequeuePendingRun();
            if (pendingRun is not null)
            {
                await RunCycleSafelyAsync(pendingRun, stoppingToken);
                continue;
            }

            try
            {
                pendingRun = await WaitForNextRunAsync(stoppingToken);
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                await RunCycleSafelyAsync(pendingRun, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Metrics sync worker stopped.");
    }

    private async Task RunCycleSafelyAsync(SyncRunStatus? requestedRun, CancellationToken stoppingToken)
    {
        var currentRun = requestedRun ?? new SyncRunStatus(Guid.NewGuid(), DateTimeOffset.UtcNow, RunningStatus);
        SetCurrentRun(currentRun);

        try
        {
            await RunCycleAsync(currentRun, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metrics sync cycle failed.");
        }
        finally
        {
            ClearCurrentRun(currentRun.Id);
        }
    }

    private async Task RunCycleAsync(SyncRunStatus runStatus, CancellationToken stoppingToken)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var discoveryService = scope.ServiceProvider.GetRequiredService<IFoundryDiscoveryService>();
        var metricsSyncService = scope.ServiceProvider.GetRequiredService<IMetricsSyncService>();
        var syncOptions = _syncOptions.CurrentValue;
        var syncRun = new SyncRun
        {
            Id = runStatus.Id,
            StartedAt = runStatus.StartedAt,
            Status = RunningStatus
        };

        dbContext.SyncRuns.Add(syncRun);
        await dbContext.SaveChangesAsync(stoppingToken);

        try
        {
            var syncTimestamp = DateTimeOffset.UtcNow;
            var discoveredHubs = await discoveryService.DiscoverHubsAsync(stoppingToken);
            var existingHubs = await dbContext.FoundryHubs.ToListAsync(stoppingToken);
            var hubsByResourceId = existingHubs.ToDictionary(hub => hub.AzureResourceId, StringComparer.OrdinalIgnoreCase);

            var summary = new SyncSummary
            {
                HubsDiscovered = discoveredHubs.Count
            };

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
                    summary.FailedHubCount++;
                    _logger.LogError(ex, "Metrics sync failed for hub {HubName} ({HubResourceId}).", hubEntity.Name, hubEntity.AzureResourceId);
                }
            }

            syncRun.CompletedAt = DateTimeOffset.UtcNow;
            syncRun.Status = summary.FailedHubCount > 0
                ? FailedStatus
                : CompletedStatus;
            syncRun.ErrorMessage = summary.FailedHubCount > 0
                ? $"{summary.FailedHubCount} hub sync operation(s) failed. See logs for details."
                : null;
            syncRun.HubsDiscovered = summary.HubsDiscovered;
            syncRun.ProjectsDiscovered = summary.ProjectsDiscovered;
            syncRun.DeploymentsDiscovered = summary.DeploymentsDiscovered;
            syncRun.AgentsDiscovered = summary.AgentsDiscovered;
            syncRun.UsageSlicesInserted = summary.UsageSlicesInserted;

            await dbContext.SaveChangesAsync(stoppingToken);

            _logger.LogInformation(
                "Metrics sync cycle completed with status {Status}. Hubs discovered: {HubsDiscovered}. Projects discovered: {ProjectsDiscovered}. Deployments discovered: {DeploymentsDiscovered}. Agents discovered: {AgentsDiscovered}. Usage slices inserted: {UsageSlicesInserted}.",
                syncRun.Status,
                summary.HubsDiscovered,
                summary.ProjectsDiscovered,
                summary.DeploymentsDiscovered,
                summary.AgentsDiscovered,
                summary.UsageSlicesInserted);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            await FinalizeFailedRunAsync(dbContext, syncRun, "Sync was canceled.");
            throw;
        }
        catch (Exception ex)
        {
            await FinalizeFailedRunAsync(dbContext, syncRun, ex.Message);
            throw;
        }
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
        summary.ProjectsDiscovered += discoveredProjects.Count;
        foreach (var discoveredProject in discoveredProjects)
        {
            if (!projectsByResourceId.TryGetValue(discoveredProject.ResourceId, out var projectEntity))
            {
                dbContext.FoundryProjects.Add(new FoundryProject
                {
                    Id = Guid.NewGuid(),
                    HubId = hubEntity.Id,
                    AzureResourceId = discoveredProject.ResourceId,
                    Name = discoveredProject.Name,
                    LastSyncedAt = syncTimestamp
                });

                summary.ProjectsInserted++;
                continue;
            }

            projectEntity.Name = discoveredProject.Name;
            projectEntity.LastSyncedAt = syncTimestamp;
            summary.ProjectsUpdated++;
        }

        var existingDeployments = await dbContext.ModelDeployments
            .Where(deployment => deployment.HubId == hubEntity.Id)
            .ToListAsync(stoppingToken);
        var deploymentsByResourceId = existingDeployments.ToDictionary(deployment => deployment.AzureResourceId, StringComparer.OrdinalIgnoreCase);

        var discoveredDeployments = await discoveryService.DiscoverDeploymentsAsync(hubEntity.AzureResourceId, stoppingToken);
        summary.DeploymentsDiscovered += discoveredDeployments.Count;
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

        var projectsForHub = await dbContext.FoundryProjects
            .Where(project => project.HubId == hubEntity.Id)
            .ToListAsync(stoppingToken);

        foreach (var projectEntity in projectsForHub)
        {
            var existingAgents = await dbContext.FoundryAgents
                .Where(agent => agent.ProjectId == projectEntity.Id)
                .ToListAsync(stoppingToken);
            var agentsByAgentId = existingAgents.ToDictionary(agent => agent.AgentId, StringComparer.OrdinalIgnoreCase);

            var discoveredAgents = await discoveryService.DiscoverAgentsAsync(hubEntity.Name, projectEntity.Name, stoppingToken);
            summary.AgentsDiscovered += discoveredAgents.Count;
            foreach (var discoveredAgent in discoveredAgents)
            {
                if (!agentsByAgentId.TryGetValue(discoveredAgent.AgentId, out var agentEntity))
                {
                    dbContext.FoundryAgents.Add(new FoundryAgent
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectEntity.Id,
                        AgentId = discoveredAgent.AgentId,
                        Name = discoveredAgent.Name,
                        Description = discoveredAgent.Description,
                        ModelName = discoveredAgent.ModelName,
                        Kind = discoveredAgent.Kind,
                        CreatedAt = discoveredAgent.CreatedAt,
                        LastSyncedAt = syncTimestamp
                    });

                    continue;
                }

                agentEntity.Name = discoveredAgent.Name;
                agentEntity.Description = discoveredAgent.Description;
                agentEntity.ModelName = discoveredAgent.ModelName;
                agentEntity.Kind = discoveredAgent.Kind;
                agentEntity.CreatedAt = discoveredAgent.CreatedAt;
                agentEntity.LastSyncedAt = syncTimestamp;
            }
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

    private async Task<SyncRunStatus?> WaitForNextRunAsync(CancellationToken stoppingToken)
    {
        var timerTask = Task.Delay(GetInterval(), stoppingToken);
        var triggerTask = _triggerChannel.Reader.WaitToReadAsync(stoppingToken).AsTask();
        var completedTask = await Task.WhenAny(timerTask, triggerTask);

        if (completedTask == triggerTask && await triggerTask)
        {
            while (_triggerChannel.Reader.TryRead(out _))
            {
            }

            return TryDequeuePendingRun();
        }

        return TryDequeuePendingRun();
    }

    private void SetCurrentRun(SyncRunStatus runStatus)
    {
        _isRunning = true;

        lock (_stateLock)
        {
            _currentRun = runStatus;
            if (_pendingRun?.Id == runStatus.Id)
            {
                _pendingRun = null;
            }
        }
    }

    private void ClearCurrentRun(Guid runId)
    {
        lock (_stateLock)
        {
            if (_currentRun?.Id == runId)
            {
                _currentRun = null;
            }
        }

        _isRunning = false;
    }

    private SyncRunStatus? TryDequeuePendingRun()
    {
        lock (_stateLock)
        {
            var pendingRun = _pendingRun;
            _pendingRun = null;
            return pendingRun;
        }
    }

    private static async Task FinalizeFailedRunAsync(BillingDbContext dbContext, SyncRun syncRun, string errorMessage)
    {
        syncRun.CompletedAt = DateTimeOffset.UtcNow;
        syncRun.Status = FailedStatus;
        syncRun.ErrorMessage = errorMessage;

        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private sealed class SyncSummary
    {
        public int HubsDiscovered { get; set; }

        public int HubsInserted { get; set; }

        public int HubsUpdated { get; set; }

        public int ProjectsDiscovered { get; set; }

        public int ProjectsInserted { get; set; }

        public int ProjectsUpdated { get; set; }

        public int DeploymentsDiscovered { get; set; }

        public int DeploymentsInserted { get; set; }

        public int DeploymentsUpdated { get; set; }

        public int AgentsDiscovered { get; set; }

        public int UsageSlicesInserted { get; set; }

        public int FailedHubCount { get; set; }
    }

    private sealed record SliceKey(Guid DeploymentId, DateTimeOffset Timestamp);
}
