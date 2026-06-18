namespace FoundryBilling.Api.Models;

public sealed record BillingMetric(
    string TenantId,
    string SubscriptionId,
    string ProjectId,
    string ProjectName,
    string ResourceGroupName,
    string ResourceName,
    decimal Cost,
    string Currency,
    DateOnly UsageDate);
