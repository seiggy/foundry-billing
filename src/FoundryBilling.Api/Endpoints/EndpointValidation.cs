namespace FoundryBilling.Api.Endpoints;

internal static class EndpointValidation
{
    public static Dictionary<string, string[]> ValidateDateRange(DateOnly? startDate, DateOnly? endDate)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (startDate.HasValue && endDate.HasValue && endDate.Value < startDate.Value)
        {
            errors["dateRange"] = ["endDate must be on or after startDate."];
        }

        return errors;
    }
}
