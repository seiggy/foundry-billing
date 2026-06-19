using FoundryBilling.Api.Models;
using FoundryBilling.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;

namespace FoundryBilling.Api.Endpoints;

public static class ProjectEndpoints
{
    public static RouteGroupBuilder MapProjectEndpoints(this RouteGroupBuilder group)
    {
        var projects = group.MapGroup("/projects")
            .WithTags("Projects");

        projects.MapGet(string.Empty, GetProjectsAsync)
            .WithName("GetProjects")
            .WithSummary("Gets synced Foundry projects.");

        projects.MapGet("/{projectId}", GetProjectAsync)
            .WithName("GetProject")
            .WithSummary("Gets a single synced Foundry project.");

        return group;
    }

    private static async Task<Ok<IReadOnlyList<FoundryProjectResponse>>> GetProjectsAsync(
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var projects = await projectService.GetProjectsAsync(cancellationToken);
        return TypedResults.Ok(projects);
    }

    private static async Task<Results<Ok<FoundryProjectResponse>, NotFound>> GetProjectAsync(
        Guid projectId,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var project = await projectService.GetProjectAsync(projectId, cancellationToken);
        return project is null ? TypedResults.NotFound() : TypedResults.Ok(project);
    }
}
