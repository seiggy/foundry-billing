using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.ResourceManager;
using Microsoft.Extensions.Options;

namespace FoundryBilling.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    private const string AzureManagementClientName = "AzureManagement";

    public static IServiceCollection AddFoundryBillingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AzureBillingOptions>(configuration.GetSection(AzureBillingOptions.SectionName));
        services.Configure<SyncOptions>(configuration.GetSection(SyncOptions.SectionName));

        services.AddSingleton<TokenCredential>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<AzureBillingOptions>>().Value;
            var credentialOptions = new DefaultAzureCredentialOptions();

            if (!string.IsNullOrWhiteSpace(options.TenantId))
            {
                credentialOptions.TenantId = options.TenantId;
            }

            // AZURE_CLIENT_ID env var tells DefaultAzureCredential which user-assigned managed identity to use
            var managedIdentityClientId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            if (!string.IsNullOrWhiteSpace(managedIdentityClientId))
            {
                credentialOptions.ManagedIdentityClientId = managedIdentityClientId;
            }

            return new DefaultAzureCredential(credentialOptions);
        });
        services.AddSingleton(serviceProvider =>
        {
            var credential = serviceProvider.GetRequiredService<TokenCredential>();
            var options = serviceProvider.GetRequiredService<IOptions<AzureBillingOptions>>().Value;

            return string.IsNullOrWhiteSpace(options.SubscriptionId)
                ? new ArmClient(credential)
                : new ArmClient(credential, options.SubscriptionId);
        });

        services.AddHttpClient(AzureManagementClientName, (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<AzureBillingOptions>>().Value;
            client.BaseAddress = new Uri(options.ManagementBaseUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddSingleton(serviceProvider =>
        {
            var credential = serviceProvider.GetRequiredService<TokenCredential>();
            return new MetricsQueryClient(credential);
        });

        return services;
    }
}
