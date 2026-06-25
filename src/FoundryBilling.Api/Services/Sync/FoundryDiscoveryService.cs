using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.AI.Projects;
using Azure.AI.Projects.Agents;
using Azure.ResourceManager;
using Azure.ResourceManager.CognitiveServices;
using Azure.ResourceManager.Resources;
using FoundryBilling.Api.Infrastructure;
using FoundryBilling.Api.Models.Sync;
using Microsoft.Extensions.Options;
using System.ClientModel;

namespace FoundryBilling.Api.Services.Sync;

public sealed class FoundryDiscoveryService : IFoundryDiscoveryService
{
    private readonly ArmClient _armClient;
    private readonly TokenCredential _credential;
    private readonly AzureBillingOptions _azureOptions;
    private readonly ILogger<FoundryDiscoveryService> _logger;

    public FoundryDiscoveryService(
        ArmClient armClient,
        TokenCredential credential,
        IOptions<AzureBillingOptions> azureOptions,
        ILogger<FoundryDiscoveryService> logger)
    {
        _armClient = armClient;
        _credential = credential;
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
                // Include both new Foundry hubs (AIServices) and legacy Azure OpenAI resources (OpenAI)
                var kind = account.Data.Kind;
                if (!string.Equals(kind, "AIServices", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(kind, "OpenAI", StringComparison.OrdinalIgnoreCase))
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

    public async Task<IReadOnlyList<DiscoveredAgent>> DiscoverAgentsAsync(
        string hubName,
        string projectName,
        CancellationToken ct = default)
    {
        try
        {
            var projectClient = new AIProjectClient(CreateProjectEndpoint(hubName, projectName), _credential);
            var discoveredAgents = new List<DiscoveredAgent>();

            await foreach (var agent in projectClient.AgentAdministrationClient.GetAgentsAsync(cancellationToken: ct))
            {
                var latestVersion = agent.GetLatestVersion();
                var modelName = latestVersion.Definition switch
                {
                    DeclarativeAgentDefinition declarative => declarative.Model,
                    _ => null
                };

                discoveredAgents.Add(new DiscoveredAgent(
                    agent.Id,
                    agent.Name,
                    latestVersion.Description,
                    modelName,
                    GetAgentKind(latestVersion.Definition),
                    latestVersion.CreatedAt));
            }

            return discoveredAgents;
        }
        catch (CredentialUnavailableException ex)
        {
            _logger.LogWarning(ex, "Azure credentials are unavailable. Foundry agent discovery is disabled for {HubName}/{ProjectName}.", hubName, projectName);
            return [];
        }
        catch (AuthenticationFailedException ex)
        {
            _logger.LogWarning(ex, "Azure authentication failed while discovering agents for {HubName}/{ProjectName}.", hubName, projectName);
            return [];
        }
        catch (ClientResultException ex) when (ex.Status is 401 or 403 or 404)
        {
            _logger.LogWarning(ex, "Azure request failed while discovering agents for {HubName}/{ProjectName}.", hubName, projectName);
            return [];
        }
    }

    private static Uri CreateProjectEndpoint(string hubName, string projectName)
    {
        // projectName may be in ARM format "accountName/projectName" — extract just the project portion
        var actualProjectName = projectName.Contains('/')
            ? projectName.Split('/').Last()
            : projectName;

        return new($"https://{hubName}.services.ai.azure.com/api/projects/{Uri.EscapeDataString(actualProjectName)}");
    }

    private static string? GetAgentKind(ProjectsAgentDefinition? definition)
        => definition switch
        {
            DeclarativeAgentDefinition => "Prompt",
            null => null,
            _ => definition.GetType().Name switch
            {
                "HostedAgentDefinition" => "Hosted",
                "WorkflowAgentDefinition" => "Workflow",
                _ => definition.GetType().Name.Replace("AgentDefinition", string.Empty, StringComparison.Ordinal)
            }
        };
}
