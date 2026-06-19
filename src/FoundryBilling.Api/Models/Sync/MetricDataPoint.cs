namespace FoundryBilling.Api.Models.Sync;

public sealed record MetricDataPoint(
    string DeploymentName,
    string ModelName,
    DateTimeOffset Timestamp,
    long PromptTokens,
    long CompletionTokens,
    long TotalTokens);
