using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FoundryBilling.Api.Tests.Fixtures;

public sealed class WebAppFixture : WebApplicationFactory<global::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Provide a connection string so Aspire's Npgsql validation passes at startup.
        // The actual DB won't be reachable, which is fine — health checks report that correctly.
        builder.UseSetting("ConnectionStrings:foundry-billing-db",
            "Host=localhost;Database=test;Username=test;Password=test");
    }

    public HttpClient CreateAppClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }
}
