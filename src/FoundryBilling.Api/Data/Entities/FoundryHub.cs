namespace FoundryBilling.Api.Data.Entities;

public sealed class FoundryHub
{
    public Guid Id { get; set; }

    public required string AzureResourceId { get; set; }

    public required string Name { get; set; }

    public required string SubscriptionId { get; set; }

    public required string ResourceGroup { get; set; }

    public required string Region { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    public List<FoundryProject> Projects { get; } = [];

    public List<ModelDeployment> Deployments { get; } = [];
}
