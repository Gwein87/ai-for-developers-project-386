using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using System.Globalization;
using Microsoft.AspNetCore.Hosting;

namespace HexletCalendar.Api.Tests;

public sealed class AvailabilityTests
{
    [Theory]
    [InlineData("2026-11-08T21:10:37.123Z", "2026-11-09", "2026-11-22", "2026-11-08T21:10:37Z")]
    [InlineData("2026-12-31T21:00:00Z", "2027-01-01", "2027-01-14", "2026-12-31T21:00:00Z")]
    public async Task WindowUsesMoscowDateAndIncludesEveryDateWithServerTimeToSeconds(string now, string first, string last, string serverNow)
    {
        using var factory = new CalendarApiFactory(DateTimeOffset.Parse(now, CultureInfo.InvariantCulture));
        using var client = factory.CreateClient();
        var id = await CreateEventTypeAsync(client, 45);
        using var response = await client.GetAsync(new Uri($"/api/event-types/{id}/availability", UriKind.Relative));
        Assert.Empty(await ApiContract.ValidateAsync(response, "/api/event-types/{id}/availability", "get"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(first, body.RootElement.GetProperty("windowStartDate").GetString());
        Assert.Equal(last, body.RootElement.GetProperty("windowEndDate").GetString());
        Assert.Equal(serverNow, body.RootElement.GetProperty("serverNow").GetString());
        var days = body.RootElement.GetProperty("days");
        Assert.Equal(14, days.GetArrayLength());
        var firstDate = DateOnly.ParseExact(first, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        for (var offset = 0; offset < 14; offset++)
        {
            Assert.Equal(firstDate.AddDays(offset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), days[offset].GetProperty("date").GetString());
            Assert.Equal(34, days[offset].GetProperty("slots").GetArrayLength());
        }
    }

    [Theory]
    [InlineData("2026-11-09T05:59:59Z", true)]
    [InlineData("2026-11-09T06:00:00Z", false)]
    [InlineData("2026-11-09T06:00:00.001Z", false)]
    public async Task StartMustBeStrictlyAfterActualServerTime(string now, bool available)
    {
        using var factory = new CalendarApiFactory(DateTimeOffset.Parse(now, CultureInfo.InvariantCulture));
        using var client = factory.CreateClient();
        var id = await CreateEventTypeAsync(client, 45);
        using var response = await client.GetAsync(new Uri($"/api/event-types/{id}/availability", UriKind.Relative));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var slots = body.RootElement.GetProperty("days")[0].GetProperty("slots");
        Assert.Equal(available, slots[0].GetProperty("isAvailable").GetBoolean());
        Assert.True(slots[1].GetProperty("isAvailable").GetBoolean());
    }

    [Theory]
    [InlineData(15, 36, "T14:45:00Z")]
    [InlineData(540, 1, "T06:00:00Z")]
    public async Task DurationBoundariesFitEntirelyWithinWorkingHours(int duration, int count, string lastStart)
    {
        using var factory = new CalendarApiFactory(new DateTimeOffset(2026, 11, 9, 5, 0, 0, TimeSpan.Zero));
        using var client = factory.CreateClient();
        var id = await CreateEventTypeAsync(client, duration);
        using var response = await client.GetAsync(new Uri($"/api/event-types/{id}/availability", UriKind.Relative));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var slots = body.RootElement.GetProperty("days")[0].GetProperty("slots");
        Assert.Equal(count, slots.GetArrayLength());
        Assert.EndsWith(lastStart, slots[count - 1].GetProperty("startAt").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("T15:00:00Z", slots[count - 1].GetProperty("endAt").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("0", HttpStatusCode.BadRequest, "validation_error")]
    [InlineData("-1", HttpStatusCode.BadRequest, "validation_error")]
    [InlineData("abc", HttpStatusCode.BadRequest, "validation_error")]
    [InlineData("1.5", HttpStatusCode.BadRequest, "validation_error")]
    [InlineData("2147483648", HttpStatusCode.BadRequest, "validation_error")]
    [InlineData("1", HttpStatusCode.NotFound, "event_type_not_found")]
    public async Task InvalidAndUnknownIdentifiersReturnDistinctContractProblems(string id, HttpStatusCode status, string code)
    {
        using var factory = new CalendarApiFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri($"/api/event-types/{id}/availability", UriKind.Relative));
        Assert.Equal(status, response.StatusCode);
        Assert.Empty(await ApiContract.ValidateAsync(response, "/api/event-types/{id}/availability", "get"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        if (status == HttpStatusCode.BadRequest)
        {
            Assert.NotEmpty(body.RootElement.GetProperty("errors").GetProperty("id").EnumerateArray());
        }
    }

    [Fact]
    public async Task MissingDatabaseReturnsInternalProblemWithoutCreatingTheDatabase()
    {
        using var factory = new CalendarApiFactory();
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.db");
        using var broken = factory.WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Calendar", $"Data Source={missing};Mode=ReadWrite;Pooling=False"));
        using var client = broken.CreateClient();
        using var response = await client.GetAsync(new Uri("/api/event-types/1/availability", UriKind.Relative));
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Empty(await ApiContract.ValidateAsync(response, "/api/event-types/{id}/availability", "get"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("internal_error", body.RootElement.GetProperty("code").GetString());
        Assert.False(File.Exists(missing));
    }

    [Theory]
    [InlineData("startAt", "2026-11-09T06:00:01Z")]
    [InlineData("startAt", "2026-11-09T06:00:00.000Z")]
    [InlineData("startAt", "2026-11-09T06:00:00+00:00")]
    [InlineData("endAt", "2026-11-09T06:45:01Z")]
    [InlineData("serverNow", "2026-11-09T05:00:00.123Z")]
    [InlineData("serverNow", "2026-11-09T05:00:00")]
    [InlineData("date", "2026-02-30")]
    [InlineData("windowStartDate", "2026-11-09T00:00:00Z")]
    public async Task ContractRejectsInvalidDateAndTimestampRepresentations(string field, string value)
    {
        using var factory = new CalendarApiFactory(new DateTimeOffset(2026, 11, 9, 5, 0, 0, TimeSpan.Zero));
        using var client = factory.CreateClient();
        var id = await CreateEventTypeAsync(client, 45);
        using var response = await client.GetAsync(new Uri($"/api/event-types/{id}/availability", UriKind.Relative));
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var target = field is "startAt" or "endAt" ? body["days"]![0]!["slots"]![0]! : field == "date" ? body["days"]![0]! : body;
        target[field] = value;
        using var incorrect = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        Assert.NotEmpty(await ApiContract.ValidateAsync(incorrect, "/api/event-types/{id}/availability", "get"));
    }

    [Fact]
    public async Task BusyIntervalsAcrossEventTypesDisableOverlapsButPermitSharedBoundaries()
    {
        using var factory = new CalendarApiFactory(new DateTimeOffset(2026, 11, 9, 5, 0, 0, TimeSpan.Zero));
        using var client = factory.CreateClient();
        var selectedId = await CreateEventTypeAsync(client, 45);
        var otherId = await CreateEventTypeAsync(client, 60);
        await factory.SeedBookingAsync(otherId,
            new DateTimeOffset(2026, 11, 9, 7, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero));
        using var response = await client.GetAsync(new Uri($"/api/event-types/{selectedId}/availability", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await ApiContract.ValidateAsync(response, "/api/event-types/{id}/availability", "get"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var slots = body.RootElement.GetProperty("days")[0].GetProperty("slots");
        Assert.True(slots[1].GetProperty("isAvailable").GetBoolean()); // 09:15–10:00 Moscow
        Assert.False(slots[2].GetProperty("isAvailable").GetBoolean()); // starts before busy interval
        Assert.False(slots[4].GetProperty("isAvailable").GetBoolean()); // contained in busy interval
        Assert.False(slots[7].GetProperty("isAvailable").GetBoolean()); // ends after busy interval
        Assert.True(slots[8].GetProperty("isAvailable").GetBoolean()); // 11:00 shared boundary
        Assert.DoesNotContain("Другой гость", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain("other@example.com", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SlotContainingAnotherTypesEntireBookingIsUnavailable()
    {
        using var factory = new CalendarApiFactory(new DateTimeOffset(2026, 11, 9, 5, 0, 0, TimeSpan.Zero));
        using var client = factory.CreateClient();
        var selectedId = await CreateEventTypeAsync(client, 90);
        var otherId = await CreateEventTypeAsync(client, 15);
        await factory.SeedBookingAsync(otherId,
            new DateTimeOffset(2026, 11, 9, 7, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 9, 7, 15, 0, TimeSpan.Zero));
        using var response = await client.GetAsync(new Uri($"/api/event-types/{selectedId}/availability", UriKind.Relative));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var slots = body.RootElement.GetProperty("days")[0].GetProperty("slots");
        Assert.False(slots[0].GetProperty("isAvailable").GetBoolean());
        Assert.True(slots[5].GetProperty("isAvailable").GetBoolean());
        Assert.Equal(3, slots[0].EnumerateObject().Count());
    }

    [Fact]
    public async Task CalendarIncludesFourteenDaysAndQuarterHourStartsForSelectedDuration()
    {
        using var factory = new CalendarApiFactory();
        using var client = factory.CreateClient();
        using var created = await client.PostAsJsonAsync(new Uri("/api/event-types", UriKind.Relative), new
        {
            name = "Консультация",
            description = "Разговор",
            durationMinutes = 45,
        });
        using var item = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = item.RootElement.GetProperty("id").GetInt32();
        using var response = await client.GetAsync(new Uri($"/api/event-types/{id}/availability", UriKind.Relative));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(id, body.RootElement.GetProperty("eventTypeId").GetInt32());
        Assert.Equal("Europe/Moscow", body.RootElement.GetProperty("timeZone").GetString());
        Assert.Equal(14, body.RootElement.GetProperty("days").GetArrayLength());
        var slots = body.RootElement.GetProperty("days")[1].GetProperty("slots");
        Assert.Equal(34, slots.GetArrayLength());
        Assert.EndsWith("T06:00:00Z", slots[0].GetProperty("startAt").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("T06:15:00Z", slots[1].GetProperty("startAt").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("T14:15:00Z", slots[33].GetProperty("startAt").GetString(), StringComparison.Ordinal);
        Assert.EndsWith("T15:00:00Z", slots[33].GetProperty("endAt").GetString(), StringComparison.Ordinal);
        Assert.All(slots.EnumerateArray(), slot => Assert.True(slot.GetProperty("isAvailable").GetBoolean()));
    }

    private static async Task<int> CreateEventTypeAsync(HttpClient client, int duration)
    {
        using var response = await client.PostAsJsonAsync(new Uri("/api/event-types", UriKind.Relative), new
        {
            name = "Консультация",
            description = "Разговор",
            durationMinutes = duration,
        }).ConfigureAwait(false);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        return body.RootElement.GetProperty("id").GetInt32();
    }
}
