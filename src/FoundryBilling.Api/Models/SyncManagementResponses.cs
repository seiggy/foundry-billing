using FoundryBilling.Api.Services.Sync;

namespace FoundryBilling.Api.Models;

public sealed record SyncStatusResponse(
    bool IsRunning,
    SyncRunStatus? CurrentRun,
    DateTimeOffset? LastCompletedAt);

public sealed record SyncHistoryResponse(IReadOnlyList<SyncRunResponse> Runs);

public sealed record SyncRunResponse(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string Status,
    string? ErrorMessage,
    int HubsDiscovered,
    int ProjectsDiscovered,
    int DeploymentsDiscovered,
    int UsageSlicesInserted);

public sealed record SyncTriggerAcceptedResponse(Guid RunId);
