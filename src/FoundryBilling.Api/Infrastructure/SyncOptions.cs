namespace FoundryBilling.Api.Infrastructure;

public sealed class SyncOptions
{
    public const string SectionName = "Sync";

    public int IntervalMinutes { get; init; } = 60;

    public int MetricLookbackHours { get; init; } = 2;
}
