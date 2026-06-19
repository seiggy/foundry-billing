namespace FoundryBilling.Api.Models;

public sealed record UsageSummaryResponse(
    int HubCount,
    int ProjectCount,
    int DeploymentCount,
    long TotalPromptTokens,
    long TotalCompletionTokens,
    long TotalTokens,
    DateTimeOffset? OldestMetric,
    DateTimeOffset? NewestMetric,
    IReadOnlyList<ModelUsageBreakdown> ByModel);

public sealed record ModelUsageBreakdown(
    string ModelName,
    long PromptTokens,
    long CompletionTokens,
    long TotalTokens);
