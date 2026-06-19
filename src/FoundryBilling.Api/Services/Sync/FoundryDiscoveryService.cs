using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.CognitiveServices;
using Azure.ResourceManager.Resources;
using FoundryBilling.Api.Infrastructure;
using FoundryBilling.Api.Models.Sync;
using Microsoft.Extensions.Options;

namespace FoundryBilling.Api.Services.Sync;

public sealed class FoundryDiscoveryService : IFoundryDiscoveryService
{
    private readonly ArmClient _armClient;
    private readonly AzureBillingOptions _azureOptions;
    private readonly ILogger<FoundryDiscoveryService> _logger;

    public FoundryDiscoveryService(
        ArmClient armClient,
        IOptions<AzureBillingOptions> azureOptions,
        ILogger<FoundryDiscoveryService> logger)
    {
        _armClient = armClient;
        _azureOptions = azureOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiscoveredHub>> DiscoverHubsAsync(CancellationToken ct = default)
    {
        var subscriptionId = _azureOptions.SubscriptionId;
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            _logger.LogWarning("Azure subscription ID is not configured. Skipping Foundry hub discovery.");
            return [];
        }

        try
        {
            var subscription = _armClient.GetSubscriptionResource(
                SubscriptionResource.CreateResourceIdentifier(subscriptionId));

            var discoveredHubs = new List<DiscoveredHub>();
            await foreach (var account in subscription.GetCognitiveServicesAccountsAsync(ct))
            {
                if (!string.Equals(account.Data.Kind, "AIServices", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                discoveredHubs.Add(new DiscoveredHub(
                    account.Id.ToString(),
                    account.Data.Name,
                    account.Id.SubscriptionId ?? subscriptionId,
                    account.Id.ResourceGroupName ?? string.Empty,
                    account.Data.Location.ToString()));
            }

            return discoveredHubs;
        }
        catch (CredentialUnavailableException ex)
        {
            _logger.LogWarning(ex, "Azure credentials are unavailable. Foundry discovery is disabled for this cycle.");
            return [];
        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogWarning(ex, "Azure authentication failed during Foundry discovery.");
            return [];
        }
        catch (RequestFailedException ex) when (ex.Status is 401 or 403)
        {
            _logger.LogWarning(ex, "Azure authorization failed during Foundry discovery.");
            return [];
        }
    }

    public async Task<IReadOnlyList<DiscoveredProject>> DiscoverProjectsAsync(
        string hubResourceId,
        CancellationToken ct = default)
    {
        try
        {
            var hubResource = _armClient.GetCognitiveServicesAccountResource(new ResourceIdentifier(hubResourceId));
            var discoveredProjects = new List<DiscoveredProject>();

            await foreach (var project in hubResource.GetCognitiveServicesProjects().GetAllAsync(ct))
            {
                discoveredProjects.Add(new DiscoveredProject(
                    project.Id.ToString(),
                    project.Data.Name,
                    project.Data.Properties.DisplayName,
                    project.Data.Location.ToString(),
                    project.Data.Properties.IsDefault ?? false));
            }

            return discoveredProjects;
        }
        catch (CredentialUnavailableException ex)
        {
            _logger.LogWarning(ex, "Azure credentials are unavailable. Foundry project discovery is disabled for {HubResourceId}.", hubResourceId);
            return [];
        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogWarning(ex, "Azure authentication failed while discovering projects for {HubResourceId}.", hubResourceId);
            return [];
        }
        catch (RequestFailedException ex) when (ex.Status is 401 or 403 or 404)
        {
            _logger.LogWarning(ex, "Azure request failed while discovering projects for {HubResourceId}.", hubResourceId);
            return [];
        }
    }

    public async Task<IReadOnlyList<DiscoveredDeployment>> DiscoverDeploymentsAsync(
        string hubResourceId,
        CancellationToken ct = default)
    {
        try
        {
            var hubResource = _armClient.GetCognitiveServicesAccountResource(new ResourceIdentifier(hubResourceId));
            var discoveredDeployments = new List<DiscoveredDeployment>();

            await foreach (var deployment in hubResource.GetCognitiveServicesAccountDeployments().GetAllAsync(ct))
            {
                var model = deployment.Data.Properties.Model;
                discoveredDeployments.Add(new DiscoveredDeployment(
                    deployment.Id.ToString(),
                    deployment.Data.Name,
                    model?.Name ?? string.Empty,
                    model?.Version,
                    deployment.Data.Sku?.Name,
                    deployment.Data.Sku?.Capacity ?? deployment.Data.Properties.CurrentCapacity ?? 0));
            }

            return discoveredDeployments;
        }
        catch (CredentialUnavailableException ex)
        {
            _logger.LogWarning(ex, "Azure credentials are unavailable. Foundry deployment discovery is disabled for {HubResourceId}.", hubResourceId);
            return [];
        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogWarning(ex, "Azure authentication failed while discovering deployments for {HubResourceId}.", hubResourceId);
            return [];
        }
        catch (RequestFailedException ex) when (ex.Status is 401 or 403 or 404)
        {
            _logger.LogWarning(ex, "Azure request failed while discovering deployments for {HubResourceId}.", hubResourceId);
            return [];
        }
    }
}
