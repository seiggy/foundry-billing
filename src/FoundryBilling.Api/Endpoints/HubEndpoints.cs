using FoundryBilling.Api.Data;
using FoundryBilling.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace FoundryBilling.Api.Endpoints;

public static class HubEndpoints
{
    public static RouteGroupBuilder MapHubEndpoints(this RouteGroupBuilder group)
    {
        var hubs = group.MapGroup("/hubs")
            .WithTags("Hubs");

        hubs.MapGet(string.Empty, GetHubsAsync)
            .WithName("GetHubs")
            .WithSummary("Gets synced Foundry hubs with project and deployment counts.");

        return group;
    }

    private static async Task<Ok<IReadOnlyList<FoundryHubResponse>>> GetHubsAsync(
        BillingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<FoundryHubResponse> hubs = await dbContext.FoundryHubs
            .AsNoTracking()
            .OrderBy(hub => hub.Name)
            .Select(hub => new FoundryHubResponse(
                hub.Id,
                hub.Name,
                hub.Region,
                hub.SubscriptionId,
                hub.Deployments.Count(),
                hub.Projects.Count(),
                hub.LastSyncedAt))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(hubs);
    }
}
