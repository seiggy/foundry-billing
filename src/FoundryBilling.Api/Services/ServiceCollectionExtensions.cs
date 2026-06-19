using FoundryBilling.Api.Services.Sync;
using FoundryBilling.Api.Workers;

namespace FoundryBilling.Api.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFoundryBillingServices(this IServiceCollection services)
    {
        services.AddScoped<IBillingService, BillingService>();
        services.AddSingleton<PtuCalculatorService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IFoundryDiscoveryService, FoundryDiscoveryService>();
        services.AddScoped<IMetricsSyncService, MetricsSyncService>();
        services.AddSingleton<MetricsSyncWorker>();
        services.AddSingleton<ISyncTriggerService>(serviceProvider => serviceProvider.GetRequiredService<MetricsSyncWorker>());
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<MetricsSyncWorker>());

        return services;
    }
}
