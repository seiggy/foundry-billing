namespace FoundryBilling.Api.Models.Sync;

public sealed record DiscoveredAgent(
    string AgentId,
    string Name,
    string? Description,
    string? ModelName,
    string? Kind,
    DateTimeOffset? CreatedAt);
