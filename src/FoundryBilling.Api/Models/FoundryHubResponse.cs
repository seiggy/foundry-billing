namespace FoundryBilling.Api.Models;

public sealed record FoundryHubResponse(
    Guid Id,
    string Name,
    string Region,
    string SubscriptionId,
    int DeploymentCount,
    int ProjectCount,
    DateTimeOffset? LastSyncedAt);
