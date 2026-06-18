using System.Net;
using FluentAssertions;
using FoundryBilling.Api.Tests.Fixtures;

namespace FoundryBilling.Api.Tests.Endpoints;

public sealed class HealthEndpointTests(WebAppFixture fixture) : IClassFixture<WebAppFixture>
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/alive")]
    public async Task Get_returns_ok_for_aspire_health_endpoints(string path)
    {
        using var client = fixture.CreateAppClient();

        var response = await client.GetAsync(path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
