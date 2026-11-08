using System.Net;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using NJsonSchema;
using NSwag;

namespace HexletCalendar.Api.Tests;

public sealed class ContractToolchainTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ValidBody =
        """{"id":7,"createdAt":"2026-11-08T09:00:00Z","detail":{"note":null}}""";

    [Theory]
    [InlineData(ValidBody)]
    [InlineData("""{"id":7,"createdAt":"2026-11-08T09:00:00+03:00","detail":{"note":"hello"}}""")]
    public async Task RealHttpResponseMatchesGeneratedContract(string body)
    {
        var errors = await GetContractErrorsAsync(body);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("""{"createdAt":"2026-11-08T09:00:00Z","detail":{"note":null}}""")]
    [InlineData("""{"id":"7","createdAt":"2026-11-08T09:00:00Z","detail":{"note":null}}""")]
    [InlineData("""{"id":7.5,"createdAt":"2026-11-08T09:00:00Z","detail":{"note":null}}""")]
    [InlineData("""{"id":2147483648,"createdAt":"2026-11-08T09:00:00Z","detail":{"note":null}}""")]
    [InlineData("""{"id":7,"createdAt":"not-a-date","detail":{"note":null}}""")]
    [InlineData("""{"id":7,"createdAt":"2026-11-08T09:00:00","detail":{"note":null}}""")]
    [InlineData("""{"id":7,"createdAt":"2026-11-08T09:00:00Z","detail":{}}""")]
    [InlineData("""{"id":7,"createdAt":"2026-11-08T09:00:00Z","detail":{"note":42}}""")]
    public async Task BrokenHttpResponseIsRejected(string body)
    {
        var errors = await GetContractErrorsAsync(body);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task UndeclaredStatusIsRejected()
    {
        var errors = await GetContractErrorsAsync(
            ValidBody, HttpStatusCode.Accepted);

        Assert.Contains("Undeclared status", errors);
    }

    [Fact]
    public async Task UndeclaredContentTypeIsRejected()
    {
        var errors = await GetContractErrorsAsync(
            ValidBody, HttpStatusCode.OK, "text/plain");

        Assert.Contains("Undeclared content type", errors);
    }

    [Fact]
    public async Task DeclaredProblemResponseIsAccepted()
    {
        var errors = await GetContractErrorsAsync(
            """{"code":"probe_conflict","message":"Probe conflict"}""",
            HttpStatusCode.Conflict,
            "application/problem+json");

        Assert.Empty(errors);
    }

    private async Task<IReadOnlyList<string>> GetContractErrorsAsync(
        string body,
        HttpStatusCode status = HttpStatusCode.OK,
        string contentType = "application/json")
    {
        using var probeFactory = factory.WithWebHostBuilder(builder =>
            builder.Configure(app => app.Run(async context =>
            {
                context.Response.StatusCode = (int)status;
                context.Response.ContentType = contentType;
                await context.Response.WriteAsync(body, Encoding.UTF8).ConfigureAwait(false);
            })));
        using var client = probeFactory.CreateClient();
        using var response = await client.GetAsync(new Uri("/probe", UriKind.Relative))
            .ConfigureAwait(false);

        var document = await OpenApiDocument.FromFileAsync(
            Path.Combine(AppContext.BaseDirectory, "ContractProbe", "openapi.json"))
            .ConfigureAwait(false);
        var operation = document.Operations.Single(
            item => item.Path == "/probe" && item.Method == "get").Operation;
        var responseKey = ((int)response.StatusCode).ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        if (!operation.Responses.TryGetValue(responseKey, out var declaredResponse))
        {
            return ["Undeclared status"];
        }
        var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (!declaredResponse.Content.TryGetValue(mediaType, out var declaredContent))
        {
            return ["Undeclared content type"];
        }
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return declaredContent.Schema.Validate(json, SchemaType.OpenApi3)
            .Select(error => error.ToString())
            .ToArray();
    }
}
