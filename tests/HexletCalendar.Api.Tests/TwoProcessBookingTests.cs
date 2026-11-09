using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;

namespace HexletCalendar.Api.Tests;

public sealed class TwoProcessBookingTests
{
    [Fact]
    public async Task TwoApiProcessesSharingOneDatabaseCannotConfirmOverlappingBookings()
    {
        var directory = Directory.CreateTempSubdirectory("hexlet-calendar-processes-").FullName;
        try
        {
            var database = Path.Combine(directory, "calendar.db");
            File.Copy(Path.Combine(RepositoryPaths.Root, "tests", "HexletCalendar.Api.Tests", "Fixtures", "calendar.db"), database);
            var firstPort = FreePort();
            var secondPort = FreePort();
            using var firstProcess = StartApi(firstPort, database);
            using var secondProcess = StartApi(secondPort, database);
            try
            {
                using var first = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{firstPort}", UriKind.Absolute) };
                using var second = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{secondPort}", UriKind.Absolute) };
                await WaitUntilHealthyAsync(first);
                await WaitUntilHealthyAsync(second);
                var firstType = await CreateEventTypeAsync(first, 60);
                var secondType = await CreateEventTypeAsync(second, 45);
                var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
                var tomorrow = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime).AddDays(1);
                var morning = new DateTimeOffset(tomorrow.ToDateTime(new TimeOnly(9, 0)), zone.GetUtcOffset(tomorrow.ToDateTime(TimeOnly.MinValue))).ToUniversalTime();
                var responses = await Task.WhenAll(
                    PostAsync(first, firstType, morning),
                    PostAsync(second, secondType, morning.AddMinutes(15))).ConfigureAwait(true);
                using var firstResponse = responses[0];
                using var secondResponse = responses[1];
                Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
                Assert.Equal(1, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
            }
            finally
            {
                Stop(firstProcess);
                Stop(secondProcess);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Process StartApi(int port, string database)
    {
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name
            ?? throw new InvalidOperationException("Test build configuration could not be determined.");
        var api = Path.Combine(RepositoryPaths.Root, "src", "HexletCalendar.Api", "bin", configuration, "net10.0", "HexletCalendar.Api.dll");
        var start = new ProcessStartInfo("dotnet", $"\"{api}\" --urls http://127.0.0.1:{port}")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Testing";
        start.Environment["ConnectionStrings__Calendar"] = $"Data Source={database};Mode=ReadWrite;Pooling=False";
        return Process.Start(start) ?? throw new InvalidOperationException("API process could not be started.");
    }

    private static async Task WaitUntilHealthyAsync(HttpClient client)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            try
            {
                using var response = await client.GetAsync(new Uri("/api/health", UriKind.Relative)).ConfigureAwait(false);
                if (response.IsSuccessStatusCode) return;
            }
            catch (HttpRequestException)
            {
                // The process is still starting.
            }
            await Task.Delay(100).ConfigureAwait(false);
        }
        throw new TimeoutException("API process did not become healthy.");
    }

    private static async Task<int> CreateEventTypeAsync(HttpClient client, int duration)
    {
        using var response = await client.PostAsJsonAsync("/api/event-types", new
        {
            name = $"Звонок {duration}",
            description = "Два процесса",
            durationMinutes = duration,
        }).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
        return body.RootElement.GetProperty("id").GetInt32();
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, int eventTypeId, DateTimeOffset start)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/bookings")
        {
            Content = JsonContent.Create(new
            {
                eventTypeId,
                startAt = start.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                guestName = "Гость",
                guestEmail = $"{Guid.NewGuid():N}@example.com",
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D", CultureInfo.InvariantCulture));
        return await client.SendAsync(request).ConfigureAwait(false);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static void Stop(Process process)
    {
        if (process.HasExited) return;
        process.Kill(entireProcessTree: true);
        process.WaitForExit();
    }
}
