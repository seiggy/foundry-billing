using Azure;
using Azure.Core;
using Azure.Monitor.Query.Models;
using Azure.ResourceManager.CognitiveServices;
using Azure.ResourceManager.CognitiveServices.Models;
using NSubstitute;

namespace FoundryBilling.Api.Tests.Services.Sync;

internal static class SyncAzureSdkTestHelper
{
    public static AsyncPageable<T> CreateAsyncPageable<T>(params T[] values)
        where T : notnull
    {
        return AsyncPageable<T>.FromPages(
        [
            Page<T>.FromValues(values, continuationToken: null, Substitute.For<Response>())
        ]);
    }

    public static Pageable<T> CreatePageable<T>(params T[] values)
        where T : notnull
    {
        return Pageable<T>.FromPages(
        [
            Page<T>.FromValues(values, continuationToken: null, Substitute.For<Response>())
        ]);
    }

    public static Response<T> CreateResponse<T>(T value)
    {
        return Response.FromValue(value, Substitute.For<Response>());
    }

    public static CognitiveServicesAccountResource CreateAccountResource(
        string subscriptionId,
        string resourceGroup,
        string accountName,
        string kind,
        string region)
    {
        var resourceId = new ResourceIdentifier(
            $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.CognitiveServices/accounts/{accountName}");
        var data = ArmCognitiveServicesModelFactory.CognitiveServicesAccountData(
            resourceId,
            accountName,
            new ResourceType("Microsoft.CognitiveServices/accounts"),
            null,
            new Dictionary<string, string>(),
            new AzureLocation(region),
            kind,
            new CognitiveServicesSku("S0"),
            null,
            null,
            null);

        var resource = Substitute.For<CognitiveServicesAccountResource>();
        resource.Id.Returns(resourceId);
        resource.Data.Returns(data);
        return resource;
    }

    public static CognitiveServicesProjectResource CreateProjectResource(
        string projectResourceId,
        string projectName,
        string displayName,
        string region,
        bool isDefault)
    {
        var projectProperties = ArmCognitiveServicesModelFactory.CognitiveServicesProjectProperties(
            null,
            null,
            displayName,
            new Dictionary<string, string>(),
            isDefault);

        var data = ArmCognitiveServicesModelFactory.CognitiveServicesProjectData(
            new ResourceIdentifier(projectResourceId),
            projectName,
            new ResourceType("Microsoft.CognitiveServices/accounts/projects"),
            null,
            new Dictionary<string, string>(),
            new AzureLocation(region),
            null,
            projectProperties,
            null);

        var resource = Substitute.For<CognitiveServicesProjectResource>();
        resource.Id.Returns(new ResourceIdentifier(projectResourceId));
        resource.Data.Returns(data);
        return resource;
    }

    public static CognitiveServicesAccountDeploymentResource CreateDeploymentResource(
        string deploymentResourceId,
        string deploymentName,
        string modelName,
        string? modelVersion,
        string? skuName,
        int currentCapacity)
    {
        var model = ArmCognitiveServicesModelFactory.CognitiveServicesAccountDeploymentModel(
            string.Empty,
            modelName,
            modelVersion ?? string.Empty,
            string.Empty,
            null);

        var properties = ArmCognitiveServicesModelFactory.CognitiveServicesAccountDeploymentProperties(
            null,
            model,
            null,
            new Dictionary<string, string>(),
            null,
            null,
            Enumerable.Empty<ServiceAccountThrottlingRule>(),
            null,
            null,
            currentCapacity,
            null,
            null);

        var sku = skuName is null
            ? null
            : new CognitiveServicesSku(skuName);

        var data = ArmCognitiveServicesModelFactory.CognitiveServicesAccountDeploymentData(
            new ResourceIdentifier(deploymentResourceId),
            deploymentName,
            new ResourceType("Microsoft.CognitiveServices/accounts/deployments"),
            null,
            sku,
            null,
            new Dictionary<string, string>(),
            properties);

        var resource = Substitute.For<CognitiveServicesAccountDeploymentResource>();
        resource.Id.Returns(new ResourceIdentifier(deploymentResourceId));
        resource.Data.Returns(data);
        return resource;
    }

    public static MetricsQueryResult CreateMetricsResult(string resourceId, params MetricResult[] metrics)
    {
        return MonitorQueryModelFactory.MetricsQueryResult(
            null,
            null,
            null,
            null,
            resourceId,
            metrics);
    }

    public static MetricResult CreateMetricResult(
        string metricName,
        params (string DeploymentName, string ModelName, DateTimeOffset Timestamp, double Total)[] points)
    {
        var series = points
            .Select(point => MonitorQueryModelFactory.MetricTimeSeriesElement(
                new Dictionary<string, string>
                {
                    ["ModelDeploymentName"] = point.DeploymentName,
                    ["ModelName"] = point.ModelName
                },
                [
                    MonitorQueryModelFactory.MetricValue(point.Timestamp, null, null, null, point.Total, null)
                ]))
            .ToArray();

        return MonitorQueryModelFactory.MetricResult(
            metricName,
            metricName,
            metricName,
            MetricUnit.Count,
            series);
    }
}
