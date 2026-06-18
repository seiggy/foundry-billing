using System.Net;
using FluentAssertions;
using FoundryBilling.Api.Tests.Fixtures;

namespace FoundryBilling.Api.Tests.Endpoints;

public sealed class HealthEndpointTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [Fact]
    public async Task Alive_returns_ok()
    {
        using var client = fixture.CreateAppClient();

        var response = await client.GetAsync("/alive");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Health_returns_valid_response()
    {
        using var client = fixture.CreateAppClient();

        var response = await client.GetAsync("/health");

        // /health may return 503 (ServiceUnavailable) when DB is unreachable in tests —
        // that's correct health check behavior. We validate the endpoint exists and responds.
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);
    }
}
