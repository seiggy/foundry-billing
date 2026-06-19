namespace FoundryBilling.Api.Data.Entities;

public sealed class FoundryAgent
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public required string AgentId { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public string? ModelName { get; set; }

    public string? Kind { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    public FoundryProject Project { get; set; } = null!;
}
