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
    private readonly FixedTimeProvider? timeProvider;

    internal CalendarApiFactory(DateTimeOffset? now = null)
    {
        timeProvider = now is { } timestamp ? new FixedTimeProvider(timestamp) : null;
        database = Path.Combine(directory, "calendar.db");
        File.Copy(Path.Combine(RepositoryPaths.Root, "tests", "HexletCalendar.Api.Tests", "Fixtures", "calendar.db"), database);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.UseSetting("ConnectionStrings:Calendar", $"Data Source={database};Mode=ReadWrite;Pooling=False");
        if (timeProvider is not null)
        {
            builder.ConfigureServices(services => services.AddSingleton<TimeProvider>(timeProvider));
        }
    }

    internal async Task<int> SeedBookingAsync(
        int eventTypeId,
        DateTimeOffset start,
        DateTimeOffset end,
        string guestName = "Другой гость",
        string guestEmail = "other@example.com")
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
            GuestName = guestName,
            GuestEmail = guestEmail,
            IdempotencyKey = Guid.NewGuid().ToString("D"),
        });
        await context.SaveChangesAsync().ConfigureAwait(false);
        return context.Bookings.Local.Single().Id;
    }

    internal async Task<int> CountBookingsAsync()
    {
        using var context = new CalendarDbContext(new DbContextOptionsBuilder<CalendarDbContext>()
            .UseSqlite($"Data Source={database};Mode=ReadWrite;Pooling=False").Options);
        return await context.Bookings.CountAsync().ConfigureAwait(false);
    }

    internal string ConnectionString => $"Data Source={database};Mode=ReadWrite;Pooling=False";

    internal void SetNow(DateTimeOffset now)
    {
        if (timeProvider is null)
        {
            throw new InvalidOperationException("This factory uses the system clock.");
        }
        timeProvider.Now = now;
    }

    internal async Task InstallFailingBookingTriggerAsync()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TRIGGER FailBooking BEFORE INSERT ON Bookings BEGIN SELECT RAISE(ABORT, 'failure'); END;";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    internal async Task DropFailingBookingTriggerAsync()
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "DROP TRIGGER FailBooking;";
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
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
        internal DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
