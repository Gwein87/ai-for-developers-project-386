using Microsoft.AspNetCore.Hosting;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;


namespace HexletCalendar.Api.Tests;

public sealed class EventTypeTests
{
    [Fact]
    public async Task OwnerCanCreateEventTypeAndGuestCanReadIt()
    {
        using var factory = new CalendarApiFactory();
        using var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync(new Uri("/api/event-types", UriKind.Relative), new
        {
            name = "Консультация",
            description = "Обсуждение проекта",
            durationMinutes = 45,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Empty(await ApiContract.ValidateAsync(created, "/api/event-types", "post"));
        using var response = await client.GetAsync(new Uri("/api/event-types", UriKind.Relative));
        Assert.Empty(await ApiContract.ValidateAsync(response, "/api/event-types", "get"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Консультация", body.RootElement.GetProperty("items")[0].GetProperty("name").GetString());
    }
    [Fact]
    public async Task TypesAreTrimmedAllowDuplicateNamesAndSurviveRestart()
    {
        using var factory = new CalendarApiFactory();
        using var client = factory.CreateClient();
        for (var index = 0; index < 2; index++)
        {
            using var response = await client.PostAsJsonAsync(new Uri("/api/event-types", UriKind.Relative), new
            {
                name = "  Консультация  ",
                description = "  Обсуждение проекта  ",
                durationMinutes = 15,
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Empty(await ApiContract.ValidateAsync(response, "/api/event-types", method: "post"));
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(index + 1, body.RootElement.GetProperty("id").GetInt32());
            Assert.Equal("Консультация", body.RootElement.GetProperty("name").GetString());
            Assert.Equal("Обсуждение проекта", body.RootElement.GetProperty("description").GetString());
        }
        using var restarted = factory.WithWebHostBuilder(_ => { });
        using var guest = restarted.CreateClient();
        using var list = await guest.GetAsync(new Uri("/api/event-types", UriKind.Relative));
        Assert.Empty(await ApiContract.ValidateAsync(list, "/api/event-types", "get"));
        using var result = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var items = result.RootElement.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(1, items[0].GetProperty("id").GetInt32());
        Assert.Equal(2, items[1].GetProperty("id").GetInt32());
        Assert.Equal("Консультация", items[1].GetProperty("name").GetString());
    }
    public static TheoryData<string, string> InvalidRequests => new()
    {
        { "{\"name\":\" \",\"description\":\"ok\",\"durationMinutes\":30}", "name" },
        { "{\"name\":null,\"description\":\"ok\",\"durationMinutes\":30}", "name" },
        { "{\"description\":\"ok\",\"durationMinutes\":30}", "name" },
        { JsonSerializer.Serialize(new { name = new string('x', 101), description = "ok", durationMinutes = 30 }), "name" },
        { "{\"name\":\"ok\",\"description\":\" \",\"durationMinutes\":30}", "description" },
        { "{\"name\":\"ok\",\"description\":null,\"durationMinutes\":30}", "description" },
        { "{\"name\":\"ok\",\"durationMinutes\":30}", "description" },
        { JsonSerializer.Serialize(new { name = "ok", description = new string('x', 2001), durationMinutes = 30 }), "description" },
        { "{\"name\":\"ok\",\"description\":\"ok\",\"durationMinutes\":0}", "durationMinutes" },
        { "{\"name\":\"ok\",\"description\":\"ok\",\"durationMinutes\":10}", "durationMinutes" },
        { "{\"name\":\"ok\",\"description\":\"ok\",\"durationMinutes\":16}", "durationMinutes" },
        { "{\"name\":\"ok\",\"description\":\"ok\",\"durationMinutes\":555}", "durationMinutes" },
        { "{\"name\":\"ok\",\"description\":\"ok\",\"durationMinutes\":15.5}", "durationMinutes" },
        { "{\"name\":\"ok\",\"description\":\"ok\",\"durationMinutes\":\"30\"}", "durationMinutes" },
        { "{\"name\":\"ok\",\"description\":\"ok\"}", "durationMinutes" },
        { "{\"id\":1,\"name\":\"ok\",\"description\":\"ok\",\"durationMinutes\":30}", "id" },
        { "{\"other\":1,\"name\":\"ok\",\"description\":\"ok\",\"durationMinutes\":30}", "other" },
        { "[]", "$" }, { "null", "$" }, { "17", "$" }, { "{", "$" },
    };

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task InvalidRequestsReturnFieldProblemsWithoutCreatingTypes(string json, string field)
    {
        using var factory = new CalendarApiFactory();
        using var client = factory.CreateClient();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(new Uri("/api/event-types", UriKind.Relative), content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await ApiContract.ValidateAsync(response, "/api/event-types", method: "post"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation_error", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("title").GetString()));
        Assert.NotEmpty(body.RootElement.GetProperty("errors").GetProperty(field).EnumerateArray());
        using var list = await client.GetAsync(new Uri("/api/event-types", UriKind.Relative));
        using var saved = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.Empty(saved.RootElement.GetProperty("items").EnumerateArray());
    }
    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    public async Task MissingDatabaseReturnsInternalProblemAndHealthStaysIndependent(string method)
    {
        using var factory = new CalendarApiFactory();
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.db");
        using var broken = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Calendar", $"Data Source={missing};Mode=ReadWrite;Pooling=False"));
        using var client = broken.CreateClient();
        using var health = await client.GetAsync(new Uri("/api/health", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal("Healthy", await health.Content.ReadAsStringAsync());
        using var response = method == "GET"
            ? await client.GetAsync(new Uri("/api/event-types", UriKind.Relative))
            : await client.PostAsJsonAsync(new Uri("/api/event-types", UriKind.Relative), new
            {
                name = "Консультация",
                description = "Обсуждение проекта",
                durationMinutes = 30,
            });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await ApiContract.ValidateAsync(response, "/api/event-types", method == "GET" ? "get" : "post"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("internal_error", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(500, body.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("title").GetString()));
        Assert.False(File.Exists(missing));
    }
    [Theory]
    [InlineData("Development", HttpStatusCode.OK)]
    [InlineData("Production", HttpStatusCode.NotFound)]
    public async Task DevelopmentServesGeneratedOpenApi(string environment, HttpStatusCode expected)
    {
        using var factory = new CalendarApiFactory();
        using var configured = factory.WithWebHostBuilder(builder => builder.UseEnvironment(environment));
        using var client = configured.CreateClient();
        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative));
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.OK)
        {
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            var artifact = Path.Combine(RepositoryPaths.Root, "src", "HexletCalendar.Contracts", "generated", "openapi.json");
            Assert.Equal(await File.ReadAllTextAsync(artifact), await response.Content.ReadAsStringAsync());
        }
    }
    [Fact]
    public async Task EmptyCalendarReturnsAnEmptyList()
    {
        using var factory = new CalendarApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/api/event-types", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await ApiContract.ValidateAsync(response, "/api/event-types", "get"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData(1, 1, 15)]
    [InlineData(100, 2000, 540)]
    public async Task InclusiveFieldBoundariesAreAccepted(int nameLength, int descriptionLength, int duration)
    {
        using var factory = new CalendarApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(new Uri("/api/event-types", UriKind.Relative), new
        {
            name = new string('я', nameLength),
            description = new string('ю', descriptionLength),
            durationMinutes = duration,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Empty(await ApiContract.ValidateAsync(response, "/api/event-types", "post"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(nameLength, body.RootElement.GetProperty("name").GetString()?.Length);
        Assert.Equal(descriptionLength, body.RootElement.GetProperty("description").GetString()?.Length);
        Assert.Equal(duration, body.RootElement.GetProperty("durationMinutes").GetInt32());
    }
}
