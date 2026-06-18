namespace FoundryBilling.Api.Data.Entities;

public sealed class ModelDeployment
{
    public Guid Id { get; set; }

    public Guid HubId { get; set; }

    public required string AzureResourceId { get; set; }

    public required string DeploymentName { get; set; }

    public required string ModelName { get; set; }

    public string? ModelVersion { get; set; }

    public string? SkuName { get; set; }

    public int Capacity { get; set; }

    public DateTimeOffset? LastSyncedAt { get; set; }

    public FoundryHub Hub { get; set; } = null!;

    public List<UsageMetricSlice> UsageMetricSlices { get; } = [];

    public List<DailyUsageRollup> DailyUsageRollups { get; } = [];
}
