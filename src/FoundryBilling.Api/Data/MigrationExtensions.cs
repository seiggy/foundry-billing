using Microsoft.EntityFrameworkCore;

namespace FoundryBilling.Api.Data;

public static class MigrationExtensions
{
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<BillingDbContext>>();

        try
        {
            logger.LogInformation("Applying database migrations...");
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to apply database migrations. The database may not be available yet.");
        }
    }
}
