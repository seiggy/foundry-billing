namespace FoundryBilling.Api.Infrastructure;

public sealed class AzureBillingOptions
{
    public const string SectionName = "Azure";

    public string? SubscriptionId { get; init; }

    public string? TenantId { get; init; }

    public string ManagementBaseUrl { get; init; } = "https://management.azure.com/";
}
