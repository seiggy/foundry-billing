using FoundryBilling.Api.Data;
using FoundryBilling.Api.Models;
using FoundryBilling.Api.Services.Sync;
using FoundryBilling.Api.Workers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace FoundryBilling.Api.Endpoints;

public static class SyncEndpoints
{
    public static RouteGroupBuilder MapSyncEndpoints(this RouteGroupBuilder group)
    {
        var sync = group.MapGroup("/sync")
            .WithTags("Sync");

        sync.MapPost("/trigger", TriggerSyncAsync)
            .WithName("TriggerSync")
            .WithSummary("Triggers a metrics sync cycle.");

        sync.MapPost("/backfill", TriggerBackfillAsync)
            .WithName("TriggerBackfill")
            .WithSummary("Triggers a full historical backfill (up to 90 days).");

        sync.MapGet("/status", GetSyncStatusAsync)
            .WithName("GetSyncStatus")
            .WithSummary("Gets the current metrics sync worker status.");

        sync.MapGet("/history", GetSyncHistoryAsync)
            .WithName("GetSyncHistory")
            .WithSummary("Gets recent metrics sync runs.");

        return group;
    }

    private static async Task<Accepted<SyncTriggerAcceptedResponse>> TriggerSyncAsync(
        MetricsSyncWorker worker,
        CancellationToken cancellationToken)
    {
        await worker.TriggerSyncAsync(cancellationToken);

        var runId = worker.PendingRun?.Id
            ?? worker.CurrentRun?.Id
            ?? Guid.Empty;

        return TypedResults.Accepted(
            uri: "/api/sync/history",
            value: new SyncTriggerAcceptedResponse(runId));
    }

    private static async Task<Accepted<SyncTriggerAcceptedResponse>> TriggerBackfillAsync(
        int? days,
        MetricsSyncWorker worker,
        CancellationToken cancellationToken)
    {
        var lookbackDays = Math.Clamp(days ?? 90, 1, 93); // Azure Monitor max retention is 93 days
        await worker.TriggerBackfillAsync(lookbackDays, cancellationToken);

        var runId = worker.PendingRun?.Id
            ?? worker.CurrentRun?.Id
            ?? Guid.Empty;

        return TypedResults.Accepted(
            uri: "/api/sync/history",
            value: new SyncTriggerAcceptedResponse(runId));
    }

    private static async Task<Ok<SyncStatusResponse>> GetSyncStatusAsync(
        ISyncTriggerService syncTriggerService,
        BillingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var lastCompletedAt = await dbContext.SyncRuns
            .AsNoTracking()
            .Where(run => run.CompletedAt != null)
            .OrderByDescending(run => run.CompletedAt)
            .Select(run => run.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return TypedResults.Ok(new SyncStatusResponse(
            syncTriggerService.IsRunning,
            syncTriggerService.CurrentRun,
            lastCompletedAt));
    }

    private static async Task<Ok<SyncHistoryResponse>> GetSyncHistoryAsync(
        BillingDbContext dbContext,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SyncRunResponse> runs = await dbContext.SyncRuns
            .AsNoTracking()
            .OrderByDescending(run => run.StartedAt)
            .Take(20)
            .Select(run => new SyncRunResponse(
                run.Id,
                run.StartedAt,
                run.CompletedAt,
                run.Status,
                run.ErrorMessage,
                run.HubsDiscovered,
                run.ProjectsDiscovered,
                run.DeploymentsDiscovered,
                run.AgentsDiscovered,
                run.UsageSlicesInserted))
            .ToListAsync(cancellationToken);

        return TypedResults.Ok(new SyncHistoryResponse(runs));
    }
}
