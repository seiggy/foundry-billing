using FoundryBilling.Api.Endpoints;
using FoundryBilling.Api.Data;
using FoundryBilling.Api.Infrastructure;
using FoundryBilling.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddNpgsqlDbContext<BillingDbContext>("foundry-billing-db");

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection(AuthOptions.SectionName));

builder.Services.AddAuthorization();

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
builder.Services.ConfigureApplicationCookie(ConfigureAuthCookie);
builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, ConfigureAuthCookie);

var app = builder.Build();

await app.MigrateDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("WebClient");
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.UseStaticFiles();

app.MapDefaultEndpoints();
app.MapFoundryBillingEndpoints();

// SPA fallback — serve index.html for non-API routes
app.MapFallbackToFile("index.html");

app.Run();

void ConfigureAuthCookie(CookieAuthenticationOptions options)
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.LoginPath = "/auth/login";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events.OnRedirectToLogin = context =>
    {
        if (IsApiAuthRequest(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (IsApiAuthRequest(context.Request.Path))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
}

static bool IsApiAuthRequest(PathString path) =>
    path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
    || path.StartsWithSegments("/auth/me", StringComparison.OrdinalIgnoreCase);

public partial class Program;

public partial class Program
{
}
