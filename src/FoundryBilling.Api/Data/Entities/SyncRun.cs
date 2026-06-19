namespace FoundryBilling.Api.Data.Entities;

public sealed class SyncRun
{
    public Guid Id { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string Status { get; set; } = "Running";

    public string? ErrorMessage { get; set; }

    public int HubsDiscovered { get; set; }

    public int ProjectsDiscovered { get; set; }

    public int DeploymentsDiscovered { get; set; }

    public int AgentsDiscovered { get; set; }

    public int UsageSlicesInserted { get; set; }
}
