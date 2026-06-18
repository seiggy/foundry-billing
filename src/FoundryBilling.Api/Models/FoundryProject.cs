namespace FoundryBilling.Api.Models;

public sealed record FoundryProject(
    string TenantId,
    string SubscriptionId,
    string ProjectId,
    string ProjectName,
    string ResourceGroupName,
    string Location,
    string? Description);
