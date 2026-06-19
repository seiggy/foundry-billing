using FoundryBilling.Api.Data;
using FoundryBilling.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FoundryBilling.Api.Services;

public sealed class ProjectService(BillingDbContext dbContext, ILogger<ProjectService> logger) : IProjectService
{
    public async Task<IReadOnlyList<FoundryProjectResponse>> GetProjectsAsync(
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Querying synced Foundry projects.");

        return await dbContext.FoundryProjects
            .AsNoTracking()
            .OrderBy(project => project.Name)
            .Select(project => new FoundryProjectResponse(
                project.Id,
                project.Name,
                project.Hub.Name,
                project.Hub.Region,
                project.LastSyncedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<FoundryProjectResponse?> GetProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Querying synced Foundry project {ProjectId}.", projectId);

        return await dbContext.FoundryProjects
            .AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new FoundryProjectResponse(
                project.Id,
                project.Name,
                project.Hub.Name,
                project.Hub.Region,
                project.LastSyncedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
