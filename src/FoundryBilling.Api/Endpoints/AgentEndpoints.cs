using FoundryBilling.Api.Data;
using FoundryBilling.Api.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace FoundryBilling.Api.Endpoints;

public static class AgentEndpoints
{
    public static RouteGroupBuilder MapAgentEndpoints(this RouteGroupBuilder group)
    {
        var agents = group.MapGroup("/agents")
            .WithTags("Agents");

        agents.MapGet(string.Empty, GetAgentsAsync)
            .WithName("GetAgents")
            .WithSummary("Gets synced Foundry agents.");

        return group;
    }

    private static async Task<Ok<IReadOnlyList<AgentResponse>>> GetAgentsAsync(
        Guid? projectId,
        Guid? hubId,
        BillingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.FoundryAgents.AsNoTracking();

        if (projectId.HasValue)
        {
            query = query.Where(agent => agent.ProjectId == projectId.Value);
        }

        if (hubId.HasValue)
        {
            query = query.Where(agent => agent.Project.HubId == hubId.Value);
        }

        IReadOnlyList<AgentResponse> agents = await query
            .OrderBy(agent => agent.Project.Hub.Name)
            .ThenBy(agent => agent.Project.Name)
            .ThenBy(agent => agent.Name)
            .Select(agent => new AgentResponse(
                agent.Id,
                agent.AgentId,
                agent.Name,
                agent.Description,
                agent.ModelName,
                agent.Kind,
                agent.Project.Name,
                agent.Project.Hub.Name,
                agent.CreatedAt,
                agent.LastSyncedAt))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(agents);
    }
}
