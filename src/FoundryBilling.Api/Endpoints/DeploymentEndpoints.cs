using FoundryBilling.Api.Data;
using FoundryBilling.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace FoundryBilling.Api.Endpoints;

public static class DeploymentEndpoints
{
    public static RouteGroupBuilder MapDeploymentEndpoints(this RouteGroupBuilder group)
    {
        var deployments = group.MapGroup("/deployments")
            .WithTags("Deployments");

        deployments.MapGet(string.Empty, GetDeploymentsAsync)
            .WithName("GetDeployments")
            .WithSummary("Gets synced deployments with recent token usage.");

        return group;
    }

    private static async Task<Ok<IReadOnlyList<DeploymentResponse>>> GetDeploymentsAsync(
        Guid? hubId,
        BillingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var query = dbContext.ModelDeployments.AsNoTracking();

        if (hubId.HasValue)
        {
            query = query.Where(deployment => deployment.HubId == hubId.Value);
        }

        IReadOnlyList<DeploymentResponse> deployments = await query
            .OrderBy(deployment => deployment.Hub.Name)
            .ThenBy(deployment => deployment.DeploymentName)
            .Select(deployment => new DeploymentResponse(
                deployment.Id,
                deployment.DeploymentName,
                deployment.ModelName,
                deployment.ModelVersion,
                deployment.Hub.Name,
                deployment.UsageMetricSlices
                    .Where(metric => metric.Timestamp >= since)
                    .Sum(metric => (long?)metric.TotalTokens) ?? 0,
                deployment.UsageMetricSlices
                    .Max(metric => (DateTimeOffset?)metric.Timestamp)))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(deployments);
    }
}
