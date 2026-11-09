using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace HexletCalendar.Api.Tests;

public sealed class BookingTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 5, 0, 0, TimeSpan.Zero);
    private const string StartAt = "2026-11-09T06:15:00Z";

    [Fact]
    public async Task GuestCreatesBookingAndSameAttemptRestoresTheCommittedResult()
    {
        using var factory = new CalendarApiFactory(Now);
        using var client = factory.CreateClient();
        var eventTypeId = await CreateEventTypeAsync(client, 45);
        var key = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);

        using var first = await PostAsync(client, key, eventTypeId, StartAt, "  Анна-Мария  ", "  Guest@Example.COM  ");
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Empty(await ApiContract.ValidateAsync(first, "/api/bookings", "post"));
        var firstBody = await first.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(firstBody);
        Assert.True(body.RootElement.GetProperty("id").GetInt32() > 0);
        Assert.Equal("Консультация", body.RootElement.GetProperty("eventTypeName").GetString());
        Assert.Equal(StartAt, body.RootElement.GetProperty("startAt").GetString());
        Assert.Equal("2026-11-09T07:00:00Z", body.RootElement.GetProperty("endAt").GetString());
        Assert.Equal("Анна-Мария", body.RootElement.GetProperty("guestName").GetString());
        Assert.Equal("Guest@Example.COM", body.RootElement.GetProperty("guestEmail").GetString());

        factory.SetNow(new DateTimeOffset(2026, 11, 9, 8, 0, 0, TimeSpan.Zero));
        using var repeated = await PostAsync(client, key, eventTypeId, StartAt, "Анна-Мария", "Guest@Example.COM");
        Assert.Equal(HttpStatusCode.Created, repeated.StatusCode);
        Assert.Equal(firstBody, await repeated.Content.ReadAsStringAsync());
        Assert.Equal(1, await factory.CountBookingsAsync());
    }

    [Fact]
    public async Task SuccessfulKeyCannotBeReusedWithDifferentNormalizedData()
    {
        using var factory = new CalendarApiFactory(Now);
        using var client = factory.CreateClient();
        var eventTypeId = await CreateEventTypeAsync(client, 45);
        var key = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        using var created = await PostAsync(client, key, eventTypeId, StartAt, "Анна", "guest@example.com");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var reused = await PostAsync(client, key, eventTypeId, StartAt, "Другая", "guest@example.com");
        Assert.Equal(HttpStatusCode.Conflict, reused.StatusCode);
        Assert.Empty(await ApiContract.ValidateAsync(reused, "/api/bookings", "post"));
        Assert.Equal("idempotency_key_reused", await CodeAsync(reused));
        Assert.Equal(1, await factory.CountBookingsAsync());
    }

    [Fact]
    public async Task UnknownEventTypeReturnsNotFoundWithoutRememberingTheAttempt()
    {
        using var factory = new CalendarApiFactory(Now);
        using var client = factory.CreateClient();
        var key = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        using var missing = await PostAsync(client, key, 404, StartAt, "Анна", "guest@example.com");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Empty(await ApiContract.ValidateAsync(missing, "/api/bookings", "post"));
        Assert.Equal("event_type_not_found", await CodeAsync(missing));
        var eventTypeId = await CreateEventTypeAsync(client, 45);
        using var retried = await PostAsync(client, key, eventTypeId, StartAt, "Анна", "guest@example.com");
        Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
    }

    [Theory]
    [InlineData("not-a-uuid", "{\"eventTypeId\":1,\"startAt\":\"2026-11-09T06:15:00Z\",\"guestName\":\"Анна\",\"guestEmail\":\"guest@example.com\"}", "idempotencyKey")]
    [InlineData("00000000-0000-0000-0000-000000000001", "{\"eventTypeId\":1,\"startAt\":\"2026-11-09T06:15:01Z\",\"guestName\":\"Анна\",\"guestEmail\":\"guest@example.com\"}", "startAt")]
    [InlineData("00000000-0000-0000-0000-000000000001", "{\"eventTypeId\":1,\"startAt\":\"2026-11-09T06:15:00Z\",\"guestName\":\"  \",\"guestEmail\":\"bad\"}", "guestName")]
    [InlineData("00000000-0000-0000-0000-000000000001", "{\"eventTypeId\":1,\"startAt\":\"2026-11-09T06:15:00Z\",\"guestName\":\"Анна\",\"guestEmail\":\"guest@example.com\",\"unknown\":1}", "unknown")]
    public async Task InvalidInputReturnsValidationProblemWithoutSaving(string key, string json, string field)
    {
        using var factory = new CalendarApiFactory(Now);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", key);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await ApiContract.ValidateAsync(response, "/api/bookings", "post"));
        Assert.Empty(await ApiContract.ValidateAsync(response, "/api/bookings", "post"));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation_error", body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty(field, out _));
        Assert.Equal(0, await factory.CountBookingsAsync());
    }

    [Theory]
    [InlineData("2026-11-09T05:00:00Z")]
    [InlineData("2026-11-09T06:01:00Z")]
    [InlineData("2026-11-09T15:00:00Z")]
    [InlineData("2026-11-23T06:00:00Z")]
    public async Task CorrectlyRepresentedButUnavailableStartReturnsInvalidSlot(string startAt)
    {
        using var factory = new CalendarApiFactory(Now);
        using var client = factory.CreateClient();
        var eventTypeId = await CreateEventTypeAsync(client, 45);
        using var response = await PostAsync(client, Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture), eventTypeId, startAt, "Анна", "guest@example.com");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_slot", await CodeAsync(response));
        Assert.Equal(0, await factory.CountBookingsAsync());
    }

    [Fact]
    public async Task ConcurrentOverlappingRequestsAcrossTypesHaveOnlyOneWinner()
    {
        using var factory = new CalendarApiFactory(Now);
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var firstType = await CreateEventTypeAsync(firstClient, 60);
        var secondType = await CreateEventTypeAsync(firstClient, 45);
        var responses = await Task.WhenAll(
            PostAsync(firstClient, Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture), firstType, "2026-11-09T06:00:00Z", "Первый", "first@example.com"),
            PostAsync(secondClient, Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture), secondType, "2026-11-09T06:15:00Z", "Второй", "second@example.com")).ConfigureAwait(true);
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        Assert.Equal(1, new[] { firstResponse, secondResponse }.Count(response => response.StatusCode == HttpStatusCode.Created));
        var conflict = new[] { firstResponse, secondResponse }.Single(response => response.StatusCode != HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("slot_taken", await CodeAsync(conflict));
        Assert.Equal(1, await factory.CountBookingsAsync());
    }

    [Fact]
    public async Task FailedConflictIsNotPersistedAndMayBeRetriedAfterTheSlotChanges()
    {
        using var factory = new CalendarApiFactory(Now);
        using var client = factory.CreateClient();
        var eventTypeId = await CreateEventTypeAsync(client, 45);
        await factory.SeedBookingAsync(eventTypeId, DateTimeOffset.Parse(StartAt, CultureInfo.InvariantCulture), DateTimeOffset.Parse("2026-11-09T07:00:00Z", CultureInfo.InvariantCulture));
        var key = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        using var conflict = await PostAsync(client, key, eventTypeId, StartAt, "Анна", "guest@example.com").ConfigureAwait(true);
        Assert.Equal("slot_taken", await CodeAsync(conflict));
        using var retried = await PostAsync(client, key, eventTypeId, "2026-11-09T07:00:00Z", "Анна", "guest@example.com").ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
        Assert.Equal(2, await factory.CountBookingsAsync());
    }

    [Fact]
    public async Task TemporaryWriteLockIsRetriedAndResourcesAreReleased()
    {
        using var factory = new CalendarApiFactory(Now);
        using var client = factory.CreateClient();
        var eventTypeId = await CreateEventTypeAsync(client, 45);
        using var blocker = new SqliteConnection(factory.ConnectionString);
        await blocker.OpenAsync();
        using var held = BeginImmediate(blocker);
        var stopwatch = Stopwatch.StartNew();
        using var response = await PostWhileReleasingLockAsync(client, held, eventTypeId).ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
        using var next = await PostAsync(client, Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture), eventTypeId, "2026-11-09T07:00:00Z", "Борис", "next@example.com").ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
    }

    [Fact]
    public async Task ExhaustedWriteLockBudgetReturnsTechnicalFailureAndNextRequestSucceeds()
    {
        using var factory = new CalendarApiFactory(Now);
        using var client = factory.CreateClient();
        var eventTypeId = await CreateEventTypeAsync(client, 45);
        using var blocker = new SqliteConnection(factory.ConnectionString);
        await blocker.OpenAsync();
        using var held = BeginImmediate(blocker);
        var stopwatch = Stopwatch.StartNew();
        using var unavailable = await PostAsync(client, Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture), eventTypeId, StartAt, "Анна", "guest@example.com").ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
        Assert.Empty(await ApiContract.ValidateAsync(unavailable, "/api/bookings", "post"));
        Assert.Equal("temporarily_unavailable", await CodeAsync(unavailable));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
        await held.RollbackAsync();
        using var next = await PostAsync(client, Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture), eventTypeId, StartAt, "Анна", "guest@example.com").ConfigureAwait(true);
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
    }

    [Fact]
    public async Task FailureBeforeCommitLeavesNoPartialSuccessAndDoesNotRememberTheKey()
    {
        using var factory = new CalendarApiFactory(Now);
        using var client = factory.CreateClient();
        var eventTypeId = await CreateEventTypeAsync(client, 45);
        var key = Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture);
        await factory.InstallFailingBookingTriggerAsync();
        using var failed = await PostAsync(client, key, eventTypeId, StartAt, "Анна", "guest@example.com");
        Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        Assert.Empty(await ApiContract.ValidateAsync(failed, "/api/bookings", "post"));
        Assert.Equal("internal_error", await CodeAsync(failed));
        Assert.Equal(0, await factory.CountBookingsAsync());
        await factory.DropFailingBookingTriggerAsync();
        using var retried = await PostAsync(client, key, eventTypeId, StartAt, "Анна", "guest@example.com");
        Assert.Equal(HttpStatusCode.Created, retried.StatusCode);
        Assert.Equal(1, await factory.CountBookingsAsync());
    }

    private static async Task<HttpResponseMessage> PostWhileReleasingLockAsync(HttpClient client, SqliteTransaction held, int eventTypeId)
    {
        var pending = PostAsync(client, Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture), eventTypeId, StartAt, "Анна", "guest@example.com");
        await Task.Delay(300).ConfigureAwait(false);
        await held.RollbackAsync().ConfigureAwait(false);
        return await pending.ConfigureAwait(false);
    }

    private static SqliteTransaction BeginImmediate(SqliteConnection connection) => connection.BeginTransaction(deferred: false);

    private static async Task<int> CreateEventTypeAsync(HttpClient client, int duration)
    {
        using var response = await client.PostAsJsonAsync("/api/event-types", new
        {
            name = "Консультация",
            description = "Разговор",
            durationMinutes = duration,
        }).ConfigureAwait(false);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        return body.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string key, int eventTypeId, string startAt, string guestName, string guestEmail)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(new { eventTypeId, startAt, guestName, guestEmail }),
        };
        request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static async Task<string?> CodeAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        return body.RootElement.GetProperty("code").GetString();
    }
}
