using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace HexletCalendar.Api.Tests;

public sealed class ApiSmokeTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task ApplicationStartsAndHealthEndpointReturnsHealthy()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(
            new Uri("/api/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
