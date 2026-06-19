namespace FoundryBilling.Api.Endpoints;

internal static class EndpointValidation
{
    private static readonly HashSet<int> AllowedAnalyticsDays = new(new[] { 30, 60, 90 });
    private static readonly HashSet<string> AllowedDeploymentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Global",
        "DataZone",
        "Regional"
    };

    public static Dictionary<string, string[]> ValidateDateRange(DateOnly? startDate, DateOnly? endDate)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
        {
            errors["dateRange"] = ["endDate must be on or after startDate."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateAnalyticsDays(int days)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (!AllowedAnalyticsDays.Contains(days))
        {
            errors["days"] = ["days must be one of 30, 60, or 90."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateDeploymentType(string? deploymentType)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(deploymentType)
            || !AllowedDeploymentTypes.Contains(deploymentType.Trim()))
        {
            errors["deploymentType"] = ["deploymentType must be one of Global, DataZone, or Regional."];
        }

        return errors;
    }
}
