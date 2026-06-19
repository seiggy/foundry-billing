using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using FluentAssertions;
using FoundryBilling.Api.Models.Sync;
using FoundryBilling.Api.Services.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FoundryBilling.Api.Tests.Services.Sync;

public sealed class MetricsSyncServiceTests
{
    [Fact]
    public async Task GetTokenUsageAsync_returns_datapoints_per_deployment()
    {
        const string resourceId = "/subscriptions/sub-123/resourceGroups/rg-a/providers/Microsoft.CognitiveServices/accounts/hub-alpha";
        var timestamp = new DateTimeOffset(2026, 06, 18, 12, 00, 00, TimeSpan.Zero);

        var queryClient = Substitute.For<MetricsQueryClient>();
        queryClient.QueryResourceAsync(
                resourceId,
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<MetricsQueryOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(SyncAzureSdkTestHelper.CreateResponse(
                SyncAzureSdkTestHelper.CreateMetricsResult(
                    resourceId,
                    SyncAzureSdkTestHelper.CreateMetricResult(
                        "ProcessedPromptTokens",
                        ("gpt-4.1-mini", "gpt-4.1-mini", timestamp, 60),
                        ("gpt-4o", "gpt-4o", timestamp, 125)),
                    SyncAzureSdkTestHelper.CreateMetricResult(
                        "GeneratedTokens",
                        ("gpt-4.1-mini", "gpt-4.1-mini", timestamp, 40),
                        ("gpt-4o", "gpt-4o", timestamp, 75)),
                    SyncAzureSdkTestHelper.CreateMetricResult(
                        "TokenTransaction",
                        ("gpt-4.1-mini", "gpt-4.1-mini", timestamp, 100),
                        ("gpt-4o", "gpt-4o", timestamp, 200)))));

        var service = new MetricsSyncService(queryClient, NullLogger<MetricsSyncService>.Instance);

        var metrics = await service.GetTokenUsageAsync(resourceId, timestamp.AddHours(-1), timestamp.AddHours(1));

        metrics.Should().BeEquivalentTo(
        [
            new MetricDataPoint("gpt-4.1-mini", "gpt-4.1-mini", timestamp, 60, 40, 100),
            new MetricDataPoint("gpt-4o", "gpt-4o", timestamp, 125, 75, 200)
        ],
        options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task GetTokenUsageAsync_handles_empty_metrics_gracefully()
    {
        const string resourceId = "/subscriptions/sub-123/resourceGroups/rg-a/providers/Microsoft.CognitiveServices/accounts/hub-alpha";

        var queryClient = Substitute.For<MetricsQueryClient>();
        queryClient.QueryResourceAsync(
                resourceId,
                Arg.Any<IEnumerable<string>>(),
                Arg.Any<MetricsQueryOptions>(),
                Arg.Any<CancellationToken>())
            .Returns(SyncAzureSdkTestHelper.CreateResponse(
                SyncAzureSdkTestHelper.CreateMetricsResult(resourceId)));

        var service = new MetricsSyncService(queryClient, NullLogger<MetricsSyncService>.Instance);

        var metrics = await service.GetTokenUsageAsync(
            resourceId,
            DateTimeOffset.UtcNow.AddHours(-2),
            DateTimeOffset.UtcNow);

        metrics.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTokenUsageAsync_uses_correct_time_range()
    {
        const string resourceId = "/subscriptions/sub-123/resourceGroups/rg-a/providers/Microsoft.CognitiveServices/accounts/hub-alpha";
        var from = new DateTimeOffset(2026, 06, 18, 00, 00, 00, TimeSpan.Zero);
        var to = from.AddHours(2);
        MetricsQueryOptions? capturedOptions = null;

        var queryClient = Substitute.For<MetricsQueryClient>();
        queryClient.QueryResourceAsync(
                resourceId,
                Arg.Any<IEnumerable<string>>(),
                Arg.Do<MetricsQueryOptions>(options => capturedOptions = options),
                Arg.Any<CancellationToken>())
            .Returns(SyncAzureSdkTestHelper.CreateResponse(
                SyncAzureSdkTestHelper.CreateMetricsResult(resourceId)));

        var service = new MetricsSyncService(queryClient, NullLogger<MetricsSyncService>.Instance);

        await service.GetTokenUsageAsync(resourceId, from, to);

        capturedOptions.Should().NotBeNull();
        capturedOptions!.TimeRange.Should().NotBeNull();
        capturedOptions.TimeRange!.Value.Start.Should().Be(from);
        capturedOptions.TimeRange!.Value.End.Should().Be(to);
        capturedOptions.Granularity.Should().Be(TimeSpan.FromHours(1));
        capturedOptions.Filter.Should().Be("ModelDeploymentName eq '*'");
        capturedOptions.Aggregations.Should().Contain(MetricAggregationType.Total);
    }
}
