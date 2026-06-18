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
            .WithSummary("Gets Foundry projects for a tenant.");

        projects.MapGet("/{projectId}", GetProjectAsync)
            .WithName("GetProject")
            .WithSummary("Gets a single Foundry project for a tenant.");

        return group;
    }

    private static async Task<Results<Ok<IReadOnlyList<FoundryProject>>, ValidationProblem>> GetProjectsAsync(
        string tenantId,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var errors = EndpointValidation.ValidateTenant(tenantId);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var projects = await projectService.GetProjectsAsync(new FoundryProjectsQuery(tenantId), cancellationToken);
        return TypedResults.Ok(projects);
    }

    private static async Task<Results<Ok<FoundryProject>, NotFound, ValidationProblem>> GetProjectAsync(
        string tenantId,
        string projectId,
        IProjectService projectService,
        CancellationToken cancellationToken)
    {
        var errors = EndpointValidation.ValidateTenant(tenantId);
        if (string.IsNullOrWhiteSpace(projectId))
        {
            errors["projectId"] = ["The projectId route parameter is required."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var project = await projectService.GetProjectAsync(new FoundryProjectLookup(tenantId, projectId), cancellationToken);
        return project is null ? TypedResults.NotFound() : TypedResults.Ok(project);
    }
}
