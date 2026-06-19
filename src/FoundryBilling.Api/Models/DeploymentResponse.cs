namespace FoundryBilling.Api.Models;

public sealed record DeploymentResponse(
    Guid Id,
    string DeploymentName,
    string ModelName,
    string? ModelVersion,
    string HubName,
    long TotalTokensLast24h,
    DateTimeOffset? LastMetricAt);
