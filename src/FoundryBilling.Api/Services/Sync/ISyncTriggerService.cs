namespace FoundryBilling.Api.Services.Sync;

public interface ISyncTriggerService
{
    Task TriggerSyncAsync(CancellationToken ct = default);

    Task TriggerBackfillAsync(int lookbackDays, CancellationToken ct = default);

    bool IsRunning { get; }

    SyncRunStatus? CurrentRun { get; }
}

public sealed record SyncRunStatus(Guid Id, DateTimeOffset StartedAt, string Status);
