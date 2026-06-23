using FoundryBilling.Api.Endpoints;
using FoundryBilling.Api.Data;
using FoundryBilling.Api.Infrastructure;
using FoundryBilling.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<BillingDbContext>("foundry-billing-db");

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("WebClient", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins is null || allowedOrigins.Length == 0)
        {
            allowedOrigins = ["http://localhost:3000", "http://localhost:5173"];
        }

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddFoundryBillingInfrastructure(builder.Configuration);
builder.Services.AddFoundryBillingServices();

var app = builder.Build();

await app.MigrateDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("WebClient");

app.UseStaticFiles();

app.MapDefaultEndpoints();
app.MapFoundryBillingEndpoints();

// SPA fallback — serve index.html for non-API routes
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;

public partial class Program
{
}
