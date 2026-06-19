namespace FoundryBilling.Api.Models.Sync;

public sealed record DiscoveredHub(
    string ResourceId,
    string Name,
    string SubscriptionId,
    string ResourceGroup,
    string Region);
