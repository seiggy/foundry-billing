namespace FoundryBilling.Api.Endpoints;

internal static class EndpointValidation
{
    public static Dictionary<string, string[]> ValidateTenant(string tenantId)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            errors["tenantId"] = ["The tenantId query parameter is required."];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateTenantAndDateRange(string tenantId, DateOnly? startDate, DateOnly? endDate)
    {
        var errors = ValidateTenant(tenantId);

        if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
        {
            errors["dateRange"] = ["endDate must be on or after startDate."];
        }

        return errors;
    }
}
