using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;

namespace HexletCalendar.Api.Tests;

public sealed class OwnerBookingTests
{
    [Fact]
    public async Task EmptyOwnerScheduleReturnsServerTimeAndMoscowZone()
    {
        using var factory = new CalendarApiFactory(DateTimeOffset.Parse("2026-11-09T08:15:37.987Z", CultureInfo.InvariantCulture));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/bookings", UriKind.Relative));

        Assert.Empty(await ApiContract.ValidateAsync(response, "/api/bookings", "get"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Europe/Moscow", body.RootElement.GetProperty("timeZone").GetString());
        Assert.Equal("2026-11-09T08:15:37Z", body.RootElement.GetProperty("serverNow").GetString());
        Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task OwnerScheduleIncludesTodayAndAllFutureBookingsSortedAcrossEventTypes()
    {
        using var factory = new CalendarApiFactory(DateTimeOffset.Parse("2026-11-09T21:30:37Z", CultureInfo.InvariantCulture));
        using var client = factory.CreateClient();
        var firstType = await CreateEventTypeAsync(client, "Разбор проекта");
        var secondType = await CreateEventTypeAsync(client, "Знакомство");
        await factory.SeedBookingAsync(firstType, Parse("2026-11-09T20:30:00Z"), Parse("2026-11-09T21:00:00Z"), "Старый гость", "old@example.com");
        var laterId = await factory.SeedBookingAsync(firstType, Parse("2026-11-11T07:00:00Z"), Parse("2026-11-11T07:30:00Z"), "Анна-Мария", "anna@example.com");
        var todayId = await factory.SeedBookingAsync(secondType, Parse("2026-11-09T21:00:00Z"), Parse("2026-11-09T21:45:00Z"), "Иван Петров", "ivan@example.com");

        using var response = await client.GetAsync(new Uri("/api/bookings", UriKind.Relative));

        Assert.Empty(await ApiContract.ValidateAsync(response, "/api/bookings", "get"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = body.RootElement.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        Assert.Equal(todayId, items[0].GetProperty("id").GetInt32());
        Assert.Equal(secondType, items[0].GetProperty("eventTypeId").GetInt32());
        Assert.Equal("Знакомство", items[0].GetProperty("eventTypeName").GetString());
        Assert.Equal("2026-11-09T21:00:00Z", items[0].GetProperty("startAt").GetString());
        Assert.Equal("2026-11-09T21:45:00Z", items[0].GetProperty("endAt").GetString());
        Assert.Equal("Иван Петров", items[0].GetProperty("guestName").GetString());
        Assert.Equal("ivan@example.com", items[0].GetProperty("guestEmail").GetString());
        Assert.Equal(laterId, items[1].GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task MissingDatabaseReturnsInternalProblemWithoutCreatingIt()
    {
        using var factory = new CalendarApiFactory();
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.db");
        using var broken = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Calendar", $"Data Source={missing};Mode=ReadWrite;Pooling=False"));
        using var client = broken.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/bookings", UriKind.Relative));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Empty(await ApiContract.ValidateAsync(response, "/api/bookings", "get"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("internal_error", body.RootElement.GetProperty("code").GetString());
        Assert.False(File.Exists(missing));
    }

    private static async Task<int> CreateEventTypeAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(new Uri("/api/event-types", UriKind.Relative), new
        {
            name,
            description = "Описание",
            durationMinutes = 30,
        }).ConfigureAwait(false);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        return body.RootElement.GetProperty("id").GetInt32();
    }

    private static DateTimeOffset Parse(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
