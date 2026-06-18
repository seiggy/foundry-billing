namespace FoundryBilling.Api.Data.Entities;

public sealed class UsageMetricSlice
{
    public Guid Id { get; set; }

    public Guid DeploymentId { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public int IntervalMinutes { get; set; } = 60;

    public long PromptTokens { get; set; }

    public long CompletionTokens { get; set; }

    public long TotalTokens { get; set; }

    public ModelDeployment Deployment { get; set; } = null!;
}
