using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using HexletCalendar.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;


namespace HexletCalendar.Api.Tests;

internal sealed class CalendarApiFactory : WebApplicationFactory<Program>
{
    private readonly string directory = Directory.CreateTempSubdirectory("hexlet-calendar-").FullName;
    private readonly string database;
    private readonly DateTimeOffset? now;

    internal CalendarApiFactory(DateTimeOffset? now = null)
    {
        this.now = now;
        database = Path.Combine(directory, "calendar.db");
        File.Copy(Path.Combine(RepositoryPaths.Root, "tests", "HexletCalendar.Api.Tests", "Fixtures", "calendar.db"), database);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.UseSetting("ConnectionStrings:Calendar", $"Data Source={database};Mode=ReadWrite;Pooling=False");
        if (now is { } timestamp)
        {
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(new FixedTimeProvider(timestamp)));
        }
    }

    internal async Task SeedBookingAsync(int eventTypeId, DateTimeOffset start, DateTimeOffset end)
    {
        using var context = new CalendarDbContext(new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite($"Data Source={database};Mode=ReadWrite;Pooling=False").Options);
        var eventType = await context.EventTypes.SingleAsync(item => item.Id == eventTypeId).ConfigureAwait(false);
        context.Bookings.Add(new BookingEntity
        {
            EventTypeId = eventTypeId,
            EventTypeName = eventType.Name,
            StartAt = start.ToUnixTimeSeconds(),
            EndAt = end.ToUnixTimeSeconds(),
            GuestName = "Другой гость",
            GuestEmail = "other@example.com",
            IdempotencyKey = Guid.NewGuid().ToString("D"),
        });
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(directory))
        {

            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (Directory.GetParent(Path.GetFullPath(directory))?.FullName != temporaryRoot
                || !Path.GetFileName(directory).StartsWith("hexlet-calendar-", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unexpected temporary database directory.");
            }
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
