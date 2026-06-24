namespace FoundryBilling.Api.Infrastructure;

public sealed class AuthOptions
{
    public const string SectionName = "AzureAd";

    public string? Instance { get; init; } = "https://login.microsoftonline.com/";

    public string? TenantId { get; init; }

    public string? ClientId { get; init; }

    public string? ClientSecret { get; init; }

    public string? CallbackPath { get; init; } = "/auth/callback";
}
