namespace FoundryBilling.Api.Models.Sync;

public sealed record DiscoveredDeployment(
    string ResourceId,
    string DeploymentName,
    string ModelName,
    string? ModelVersion,
    string? SkuName,
    int Capacity);
