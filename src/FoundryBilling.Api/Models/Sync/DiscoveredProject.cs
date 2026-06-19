namespace FoundryBilling.Api.Models.Sync;

public sealed record DiscoveredProject(
    string ResourceId,
    string Name,
    string? DisplayName,
    string Region,
    bool IsDefault);
