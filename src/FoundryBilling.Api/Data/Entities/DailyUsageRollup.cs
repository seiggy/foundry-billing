namespace FoundryBilling.Api.Data.Entities;

public sealed class DailyUsageRollup
{
    public Guid Id { get; set; }

    public Guid DeploymentId { get; set; }

    public DateOnly Date { get; set; }

    public long PromptTokens { get; set; }

    public long CompletionTokens { get; set; }

    public long TotalTokens { get; set; }

    public ModelDeployment Deployment { get; set; } = null!;
}
