namespace FoundryBilling.Api.Models;

public sealed record AgentResponse(
    Guid Id,
    string AgentId,
    string Name,
    string? Description,
    string? ModelName,
    string? Kind,
    string ProjectName,
    string HubName,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? LastSyncedAt);
