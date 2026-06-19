using Azure;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using FoundryBilling.Api.Models.Sync;

namespace FoundryBilling.Api.Services.Sync;

public sealed class MetricsSyncService : IMetricsSyncService
{
    private const string MetricNamespace = "Microsoft.CognitiveServices/accounts";
    private const string DeploymentDimensionName = "ModelDeploymentName";
    private const string ModelDimensionName = "ModelName";
    private static readonly string[] MetricNames =
    [
        "ProcessedPromptTokens",
        "GeneratedTokens",
        "TokenTransaction"
    ];

    private readonly MetricsQueryClient _metricsQueryClient;
    private readonly ILogger<MetricsSyncService> _logger;

    public MetricsSyncService(MetricsQueryClient metricsQueryClient, ILogger<MetricsSyncService> logger)
    {
        _metricsQueryClient = metricsQueryClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<MetricDataPoint>> GetTokenUsageAsync(
        string resourceId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        if (to <= from)
        {
            return [];
        }

        try
        {
            var options = new MetricsQueryOptions
            {
                TimeRange = new QueryTimeRange(from, to),
                Granularity = TimeSpan.FromHours(1),
                Filter = $"{DeploymentDimensionName} eq '*'",
                MetricNamespace = MetricNamespace
            };

            options.Aggregations.Add(MetricAggregationType.Total);

            var response = await _metricsQueryClient.QueryResourceAsync(resourceId, MetricNames, options, ct);
            var buckets = new Dictionary<MetricPointKey, MetricPointAccumulator>();

            foreach (var metric in response.Value.Metrics)
            {
                var metricName = metric.Name;
                foreach (var series in metric.TimeSeries)
                {
                    var deploymentName = GetMetadataValue(series, DeploymentDimensionName);
                    if (string.IsNullOrWhiteSpace(deploymentName))
                    {
                        continue;
                    }

                    var modelName = GetMetadataValue(series, ModelDimensionName) ?? string.Empty;
                    foreach (var value in series.Values)
                    {
                        var key = new MetricPointKey(deploymentName, modelName, value.TimeStamp);
                        if (!buckets.TryGetValue(key, out var bucket))
                        {
                            bucket = new MetricPointAccumulator();
                            buckets[key] = bucket;
                        }

                        var metricValue = ToTokenCount(value.Total);
                        if (string.Equals(metricName, "ProcessedPromptTokens", StringComparison.OrdinalIgnoreCase))
                        {
                            bucket.PromptTokens = metricValue;
                        }
                        else if (string.Equals(metricName, "GeneratedTokens", StringComparison.OrdinalIgnoreCase))
                        {
                            bucket.CompletionTokens = metricValue;
                        }
                        else if (string.Equals(metricName, "TokenTransaction", StringComparison.OrdinalIgnoreCase))
                        {
                            bucket.TotalTokens = metricValue;
                        }
                    }
                }
            }

            return buckets
                .Select(pair => new MetricDataPoint(
                    pair.Key.DeploymentName,
                    pair.Key.ModelName,
                    pair.Key.Timestamp,
                    pair.Value.PromptTokens,
                    pair.Value.CompletionTokens,
                    pair.Value.TotalTokens > 0
                        ? pair.Value.TotalTokens
                        : pair.Value.PromptTokens + pair.Value.CompletionTokens))
                .OrderBy(point => point.Timestamp)
                .ThenBy(point => point.DeploymentName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (CredentialUnavailableException ex)
        {
            _logger.LogWarning(ex, "Azure credentials are unavailable. Metrics sync is disabled for {ResourceId}.", resourceId);
            return [];
        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogWarning(ex, "Azure authentication failed while querying metrics for {ResourceId}.", resourceId);
            return [];
        }
        catch (RequestFailedException ex) when (ex.Status is 401 or 403 or 404)
        {
            _logger.LogWarning(ex, "Azure metrics query failed for {ResourceId}.", resourceId);
            return [];
        }
    }

    private static string? GetMetadataValue(MetricTimeSeriesElement series, string name)
    {
        foreach (var metadataValue in series.Metadata)
        {
            if (string.Equals(metadataValue.Key, name, StringComparison.OrdinalIgnoreCase))
            {
                return metadataValue.Value;
            }
        }

        return null;
    }

    private static long ToTokenCount(double? value)
        => value is null
            ? 0
            : Convert.ToInt64(Math.Round(value.Value, MidpointRounding.AwayFromZero));

    private sealed record MetricPointKey(string DeploymentName, string ModelName, DateTimeOffset Timestamp);

    private sealed class MetricPointAccumulator
    {
        public long PromptTokens { get; set; }

        public long CompletionTokens { get; set; }

        public long TotalTokens { get; set; }
    }
}
