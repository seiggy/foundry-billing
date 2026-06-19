namespace FoundryBilling.Api.Data.Entities;

public sealed class FoundryProject
{
    public Guid Id { get; set; }

    public Guid HubId { get; set; }

    public required string AzureResourceId { get; set; }

    public required string Name { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    public FoundryHub Hub { get; set; } = null!;

    public List<FoundryAgent> Agents { get; } = [];
}
