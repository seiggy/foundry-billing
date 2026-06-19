using System.Reflection;
using FluentAssertions;
using FoundryBilling.Api.Data;
using FoundryBilling.Api.Data.Entities;
using FoundryBilling.Api.Infrastructure;
using FoundryBilling.Api.Models.Sync;
using FoundryBilling.Api.Services.Sync;
using FoundryBilling.Api.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FoundryBilling.Api.Tests.Services.Sync;

public sealed class MetricsSyncWorkerTests
{
    [Fact]
    public async Task ExecuteAsync_discovers_hubs_and_stores_them()
    {
        await using var harness = new WorkerHarness();

        harness.DiscoveryService.DiscoverHubsAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new DiscoveredHub("/subscriptions/sub-a/resourceGroups/rg-a/providers/Microsoft.CognitiveServices/accounts/hub-alpha", "hub-alpha", "sub-a", "rg-a", "eastus"),
            new DiscoveredHub("/subscriptions/sub-b/resourceGroups/rg-b/providers/Microsoft.CognitiveServices/accounts/hub-beta", "hub-beta", "sub-b", "rg-b", "westus")
        ]);
        harness.DiscoveryService.DiscoverProjectsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        harness.DiscoveryService.DiscoverDeploymentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        harness.MetricsService.GetTokenUsageAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns([]);

        await harness.ExecuteCycleAsync();

        await using var dbContext = harness.CreateDbContext();
        var hubs = await dbContext.FoundryHubs
            .AsNoTracking()
            .OrderBy(hub => hub.Name)
            .ToListAsync();

        hubs.Should().HaveCount(2);
        hubs.Select(hub => hub.AzureResourceId).Should().BeEquivalentTo(
        [
            "/subscriptions/sub-a/resourceGroups/rg-a/providers/Microsoft.CognitiveServices/accounts/hub-alpha",
            "/subscriptions/sub-b/resourceGroups/rg-b/providers/Microsoft.CognitiveServices/accounts/hub-beta"
        ]);
    }

    [Fact]
    public async Task ExecuteAsync_discovers_deployments_per_hub()
    {
        await using var harness = new WorkerHarness();

        const string hubAlphaResourceId = "/subscriptions/sub-a/resourceGroups/rg-a/providers/Microsoft.CognitiveServices/accounts/hub-alpha";
        const string hubBetaResourceId = "/subscriptions/sub-b/resourceGroups/rg-b/providers/Microsoft.CognitiveServices/accounts/hub-beta";

        harness.DiscoveryService.DiscoverHubsAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new DiscoveredHub(hubAlphaResourceId, "hub-alpha", "sub-a", "rg-a", "eastus"),
            new DiscoveredHub(hubBetaResourceId, "hub-beta", "sub-b", "rg-b", "westus")
        ]);
        harness.DiscoveryService.DiscoverProjectsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        harness.DiscoveryService.DiscoverDeploymentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var hubResourceId = callInfo.ArgAt<string>(0);
                return hubResourceId == hubAlphaResourceId
                    ? new[]
                    {
                        new DiscoveredDeployment($"{hubAlphaResourceId}/deployments/gpt-4o", "gpt-4o", "gpt-4o", "2024-05-13", "Standard", 2)
                    }
                    : new[]
                    {
                        new DiscoveredDeployment($"{hubBetaResourceId}/deployments/gpt-4.1-mini", "gpt-4.1-mini", "gpt-4.1-mini", "2024-06-01", "Global", 1)
                    };
            });
        harness.MetricsService.GetTokenUsageAsync(Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns([]);

        await harness.ExecuteCycleAsync();

        await using var dbContext = harness.CreateDbContext();
        var deployments = await dbContext.ModelDeployments
            .AsNoTracking()
            .Include(deployment => deployment.Hub)
            .OrderBy(deployment => deployment.DeploymentName)
            .ToListAsync();

        deployments.Should().HaveCount(2);
        deployments.Select(deployment => deployment.Hub.AzureResourceId).Should().BeEquivalentTo([hubAlphaResourceId, hubBetaResourceId]);
        _ = harness.DiscoveryService.Received(1).DiscoverDeploymentsAsync(hubAlphaResourceId, Arg.Any<CancellationToken>());
        _ = harness.DiscoveryService.Received(1).DiscoverDeploymentsAsync(hubBetaResourceId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_fetches_metrics_and_stores_slices()
    {
        await using var harness = new WorkerHarness();

        const string hubResourceId = "/subscriptions/sub-a/resourceGroups/rg-a/providers/Microsoft.CognitiveServices/accounts/hub-alpha";
        var firstTimestamp = new DateTimeOffset(2026, 06, 18, 00, 00, 00, TimeSpan.Zero);
        var secondTimestamp = firstTimestamp.AddHours(1);

        harness.DiscoveryService.DiscoverHubsAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new DiscoveredHub(hubResourceId, "hub-alpha", "sub-a", "rg-a", "eastus")
        ]);
        harness.DiscoveryService.DiscoverProjectsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        harness.DiscoveryService.DiscoverDeploymentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
        [
            new DiscoveredDeployment($"{hubResourceId}/deployments/gpt-4o", "gpt-4o", "gpt-4o", "2024-05-13", "Standard", 2)
        ]);
        harness.MetricsService.GetTokenUsageAsync(hubResourceId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(
        [
            new MetricDataPoint("gpt-4o", "gpt-4o", firstTimestamp, 125, 75, 200),
            new MetricDataPoint("gpt-4o", "gpt-4o", secondTimestamp, 180, 120, 300)
        ]);

        await harness.ExecuteCycleAsync();

        await using var dbContext = harness.CreateDbContext();
        var slices = await dbContext.UsageMetricSlices
            .AsNoTracking()
            .OrderBy(slice => slice.Timestamp)
            .ToListAsync();

        slices.Should().HaveCount(2);
        slices.Select(slice => slice.TotalTokens).Should().BeEquivalentTo([200L, 300L], options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task ExecuteAsync_handles_discovery_failure_gracefully()
    {
        await using var harness = new WorkerHarness();

        harness.DiscoveryService.DiscoverHubsAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException<IReadOnlyList<DiscoveredHub>>(new InvalidOperationException("ARM query failed.")));

        var act = () => harness.ExecuteCycleSafelyAsync();

        await act.Should().NotThrowAsync();

        await using var dbContext = harness.CreateDbContext();
        (await dbContext.FoundryHubs.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_skips_already_synced_within_interval()
    {
        await using var harness = new WorkerHarness();

        const string hubResourceId = "/subscriptions/sub-a/resourceGroups/rg-a/providers/Microsoft.CognitiveServices/accounts/hub-alpha";
        const string deploymentResourceId = "/subscriptions/sub-a/resourceGroups/rg-a/providers/Microsoft.CognitiveServices/accounts/hub-alpha/deployments/gpt-4o";
        var timestamp = DateTimeOffset.UtcNow.AddMinutes(-5);

        await using (var dbContext = harness.CreateDbContext())
        {
            var hub = new FoundryHub
            {
                Id = Guid.NewGuid(),
                AzureResourceId = hubResourceId,
                Name = "hub-alpha",
                SubscriptionId = "sub-a",
                ResourceGroup = "rg-a",
                Region = "eastus",
                LastSyncedAt = DateTimeOffset.UtcNow
            };

            var deployment = new ModelDeployment
            {
                Id = Guid.NewGuid(),
                HubId = hub.Id,
                AzureResourceId = deploymentResourceId,
                DeploymentName = "gpt-4o",
                ModelName = "gpt-4o",
                ModelVersion = "2024-05-13",
                SkuName = "Standard",
                Capacity = 2,
                LastSyncedAt = DateTimeOffset.UtcNow
            };

            dbContext.FoundryHubs.Add(hub);
            dbContext.ModelDeployments.Add(deployment);
            dbContext.UsageMetricSlices.Add(new UsageMetricSlice
            {
                Id = Guid.NewGuid(),
                DeploymentId = deployment.Id,
                Timestamp = timestamp,
                IntervalMinutes = 60,
                PromptTokens = 100,
                CompletionTokens = 40,
                TotalTokens = 140
            });

            await dbContext.SaveChangesAsync();
        }

        harness.DiscoveryService.DiscoverHubsAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new DiscoveredHub(hubResourceId, "hub-alpha", "sub-a", "rg-a", "eastus")
        ]);
        harness.DiscoveryService.DiscoverProjectsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
        harness.DiscoveryService.DiscoverDeploymentsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
        [
            new DiscoveredDeployment(deploymentResourceId, "gpt-4o", "gpt-4o", "2024-05-13", "Standard", 2)
        ]);
        harness.MetricsService.GetTokenUsageAsync(hubResourceId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>()).Returns(
        [
            new MetricDataPoint("gpt-4o", "gpt-4o", timestamp, 100, 40, 140)
        ]);

        await harness.ExecuteCycleAsync();

        await using var verificationContext = harness.CreateDbContext();
        (await verificationContext.UsageMetricSlices.CountAsync()).Should().Be(1);
    }

    private sealed class WorkerHarness : IAsyncDisposable
    {
        private static readonly MethodInfo RunCycleAsyncMethod = typeof(MetricsSyncWorker)
            .GetMethod("RunCycleAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        private static readonly MethodInfo RunCycleSafelyAsyncMethod = typeof(MetricsSyncWorker)
            .GetMethod("RunCycleSafelyAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private readonly DbContextOptions<BillingDbContext> _dbOptions;
        private readonly ServiceProvider _provider;

        public WorkerHarness()
        {
            DiscoveryService = Substitute.For<IFoundryDiscoveryService>();
            MetricsService = Substitute.For<IMetricsSyncService>();

            _dbOptions = new DbContextOptionsBuilder<BillingDbContext>()
                .UseInMemoryDatabase($"metrics-sync-worker-{Guid.NewGuid():N}")
                .Options;

            var services = new ServiceCollection();
            services.AddScoped(_ => new BillingDbContext(_dbOptions));
            services.AddScoped(_ => DiscoveryService);
            services.AddScoped(_ => MetricsService);
            _provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });

            var optionsMonitor = Substitute.For<IOptionsMonitor<SyncOptions>>();
            optionsMonitor.CurrentValue.Returns(new SyncOptions { IntervalMinutes = 60, MetricLookbackHours = 2 });

            Worker = new MetricsSyncWorker(
                _provider.GetRequiredService<IServiceScopeFactory>(),
                optionsMonitor,
                NullLogger<MetricsSyncWorker>.Instance);
        }

        public IFoundryDiscoveryService DiscoveryService { get; }

        public IMetricsSyncService MetricsService { get; }

        public MetricsSyncWorker Worker { get; }

        public BillingDbContext CreateDbContext() => new(_dbOptions);

        public async Task ExecuteCycleAsync(CancellationToken cancellationToken = default)
        {
            var task = (Task)RunCycleAsyncMethod.Invoke(Worker, [cancellationToken])!;
            await task;
        }

        public async Task ExecuteCycleSafelyAsync(CancellationToken cancellationToken = default)
        {
            var task = (Task)RunCycleSafelyAsyncMethod.Invoke(Worker, [cancellationToken])!;
            await task;
        }

        public ValueTask DisposeAsync()
        {
            return _provider.DisposeAsync();
        }
    }
}
