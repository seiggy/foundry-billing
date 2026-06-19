namespace FoundryBilling.Api.Models;

public sealed record BillingMetricResponse(
    string DeploymentName,
    string ModelName,
    string? ModelVersion,
    string HubName,
    DateTimeOffset Timestamp,
    long PromptTokens,
    long CompletionTokens,
    long TotalTokens);
