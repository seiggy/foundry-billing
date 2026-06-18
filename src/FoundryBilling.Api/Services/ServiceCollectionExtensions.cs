namespace FoundryBilling.Api.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFoundryBillingServices(this IServiceCollection services)
    {
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<IProjectService, ProjectService>();

        return services;
    }
}
