using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.CognitiveServices;
using Azure.ResourceManager.CognitiveServices.Mocking;
using Azure.ResourceManager.Resources;
using FluentAssertions;
using FoundryBilling.Api.Infrastructure;
using FoundryBilling.Api.Models.Sync;
using FoundryBilling.Api.Services.Sync;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FoundryBilling.Api.Tests.Services.Sync;

public sealed class FoundryDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverHubsAsync_returns_only_AIServices_accounts()
    {
        const string subscriptionId = "11111111-1111-1111-1111-111111111111";

        var accountResources = new[]
        {
            SyncAzureSdkTestHelper.CreateAccountResource(subscriptionId, "rg-a", "hub-alpha", "AIServices", "eastus"),
            SyncAzureSdkTestHelper.CreateAccountResource(subscriptionId, "rg-b", "speech-alpha", "SpeechServices", "westus")
        };
        var subscription = new TestSubscriptionResource(subscriptionId, accountResources, hubResource: null);
        var armClient = new TestArmClient(subscription, hubResource: null);

        var service = CreateService(armClient, subscriptionId);

        var discoveredHubs = await service.DiscoverHubsAsync();

        discoveredHubs.Should().ContainSingle();
        var hub = discoveredHubs[0];
        hub.ResourceId.Should().Be("/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/rg-a/providers/Microsoft.CognitiveServices/accounts/hub-alpha");
        hub.Name.Should().Be("hub-alpha");
        hub.SubscriptionId.Should().Be("11111111-1111-1111-1111-111111111111");
        hub.ResourceGroup.Should().Be("rg-a");
        hub.Region.Should().Be("eastus");
    }

    [Fact]
    public async Task DiscoverDeploymentsAsync_returns_deployment_details()
    {
        const string subscriptionId = "11111111-1111-1111-1111-111111111111";
        const string hubResourceId = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/rg-a/providers/Microsoft.CognitiveServices/accounts/hub-alpha";
        const string deploymentResourceId = "/subscriptions/11111111-1111-1111-1111-111111111111/resourceGroups/rg-a/providers/Microsoft.CognitiveServices/accounts/hub-alpha/deployments/gpt-4o";

        var deploymentResources = new[]
        {
            SyncAzureSdkTestHelper.CreateDeploymentResource(
                deploymentResourceId,
                "gpt-4o",
                "gpt-4o",
                "2024-05-13",
                "Standard",
                currentCapacity: 4)
        };
        var hub = new TestAccountResource(deploymentResources);
        var subscription = new TestSubscriptionResource(subscriptionId, [], hub);
        var armClient = new TestArmClient(subscription, hub);

        var service = CreateService(armClient, subscriptionId);

        var discoveredDeployments = await service.DiscoverDeploymentsAsync(hubResourceId);

        discoveredDeployments.Should().ContainSingle();
        var deployment = discoveredDeployments[0];
        deployment.ResourceId.Should().Be(deploymentResourceId);
        deployment.DeploymentName.Should().Be("gpt-4o");
        deployment.ModelName.Should().Be("gpt-4o");
        deployment.ModelVersion.Should().Be("2024-05-13");
        deployment.SkuName.Should().Be("Standard");
        deployment.Capacity.Should().Be(4);
    }

    private static FoundryDiscoveryService CreateService(ArmClient armClient, string subscriptionId)
    {
        var options = Substitute.For<IOptions<AzureBillingOptions>>();
        options.Value.Returns(new AzureBillingOptions { SubscriptionId = subscriptionId });
        var credential = Substitute.For<TokenCredential>();

        return new FoundryDiscoveryService(
            armClient,
            credential,
            options,
            NullLogger<FoundryDiscoveryService>.Instance);
    }

    private sealed class TestArmClient(SubscriptionResource? subscription, CognitiveServicesAccountResource? hubResource) : ArmClient
    {
        public override SubscriptionResource GetSubscriptionResource(ResourceIdentifier id)
        {
            return subscription ?? throw new InvalidOperationException("No subscription resource configured.");
        }

        public override T GetCachedClient<T>(Func<ArmClient, T> factory)
        {
            if (typeof(T) == typeof(MockableCognitiveServicesArmClient) && hubResource is not null)
            {
                return (T)(object)new TestMockableArmClient(hubResource);
            }

            return factory(this);
        }

        public override T GetResourceClient<T>(Func<T> factory)
        {
            if (typeof(T) == typeof(CognitiveServicesAccountResource) && hubResource is not null)
            {
                return (T)(object)hubResource;
            }

            return factory();
        }
    }

    private sealed class TestSubscriptionResource(
        string subscriptionId,
        IReadOnlyList<CognitiveServicesAccountResource> accounts,
        CognitiveServicesAccountResource? hubResource) : SubscriptionResource
    {
        private readonly ResourceIdentifier _id = SubscriptionResource.CreateResourceIdentifier(subscriptionId);
        private readonly TestMockableSubscriptionResource _mockable = new(accounts, hubResource);

        public override ResourceIdentifier Id => _id;

        public override T GetCachedClient<T>(Func<ArmClient, T> factory)
        {
            if (typeof(T) == typeof(MockableCognitiveServicesSubscriptionResource))
            {
                return (T)(object)_mockable;
            }

            throw new NotSupportedException($"Unsupported cached client type: {typeof(T).FullName}");
        }
    }

    private sealed class TestMockableSubscriptionResource(
        IReadOnlyList<CognitiveServicesAccountResource> accounts,
        CognitiveServicesAccountResource? hubResource) : MockableCognitiveServicesSubscriptionResource
    {
        private readonly IReadOnlyList<CognitiveServicesAccountResource> _accounts = accounts;
        private readonly CognitiveServicesAccountResource? _hubResource = hubResource;

        public override Pageable<CognitiveServicesAccountResource> GetCognitiveServicesAccounts(CancellationToken cancellationToken = default)
        {
            return SyncAzureSdkTestHelper.CreatePageable(_accounts.ToArray());
        }

        public override AsyncPageable<CognitiveServicesAccountResource> GetCognitiveServicesAccountsAsync(CancellationToken cancellationToken = default)
        {
            return SyncAzureSdkTestHelper.CreateAsyncPageable(_accounts.ToArray());
        }
    }

    private sealed class TestAccountResource(IReadOnlyList<CognitiveServicesAccountDeploymentResource> deployments) : CognitiveServicesAccountResource
    {
        private readonly TestDeploymentCollection _deployments = new(deployments);
        private readonly TestProjectCollection _projects = new([]);

        public override CognitiveServicesProjectCollection GetCognitiveServicesProjects()
        {
            return _projects;
        }

        public override CognitiveServicesAccountDeploymentCollection GetCognitiveServicesAccountDeployments()
        {
            return _deployments;
        }
    }

    private sealed class TestMockableArmClient(CognitiveServicesAccountResource hubResource) : MockableCognitiveServicesArmClient
    {
        public override CognitiveServicesAccountResource GetCognitiveServicesAccountResource(ResourceIdentifier id)
        {
            return hubResource;
        }
    }

    private sealed class TestProjectCollection(IReadOnlyList<CognitiveServicesProjectResource> projects) : CognitiveServicesProjectCollection
    {
        public override AsyncPageable<CognitiveServicesProjectResource> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return SyncAzureSdkTestHelper.CreateAsyncPageable(projects.ToArray());
        }
    }

    private sealed class TestDeploymentCollection(IReadOnlyList<CognitiveServicesAccountDeploymentResource> deployments) : CognitiveServicesAccountDeploymentCollection
    {
        public override AsyncPageable<CognitiveServicesAccountDeploymentResource> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return SyncAzureSdkTestHelper.CreateAsyncPageable(deployments.ToArray());
        }
    }
}
