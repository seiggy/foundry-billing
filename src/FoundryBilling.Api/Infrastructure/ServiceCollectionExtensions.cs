using System.Net.Http.Headers;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Options;

namespace FoundryBilling.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    private const string AzureManagementClientName = "AzureManagement";

    public static IServiceCollection AddFoundryBillingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AzureBillingOptions>(configuration.GetSection(AzureBillingOptions.SectionName));

        services.AddSingleton<DefaultAzureCredential>();
        services.AddSingleton<TokenCredential>(serviceProvider => serviceProvider.GetRequiredService<DefaultAzureCredential>());
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

        return services;
    }
}
