using System.Globalization;
using HexletCalendar.Contracts.Generated;
using HexletCalendar.Data;
using Microsoft.EntityFrameworkCore;

namespace HexletCalendar.Api.Http;

internal static class BookingsEndpoint
{
    internal static async Task<IResult> GetAsync(CalendarDbContext database, TimeProvider clock, CancellationToken cancellation)
    {
        var now = clock.GetUtcNow();
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var localMidnight = localDate.ToDateTime(TimeOnly.MinValue);
        var lowerBound = new DateTimeOffset(localMidnight, zone.GetUtcOffset(localMidnight)).ToUnixTimeSeconds();
        var entities = await database.Bookings.AsNoTracking()
            .Where(item => item.StartAt >= lowerBound)
            .OrderBy(item => item.StartAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellation).ConfigureAwait(false);
        var items = entities.Select(item => new Booking
        {
            Id = item.Id,
            EventTypeId = item.EventTypeId,
            EventTypeName = item.EventTypeName,
            StartAt = FormatUtc(item.StartAt),
            EndAt = FormatUtc(item.EndAt),
            GuestName = item.GuestName,
            GuestEmail = item.GuestEmail,
        }).ToList();
        return Results.Ok(new Bookings
        {
            TimeZone = "Europe/Moscow",
            ServerNow = FormatUtc(now.ToUnixTimeSeconds()),
            Items = items,
        });
    }

    private static string FormatUtc(long unixSeconds) => DateTimeOffset.FromUnixTimeSeconds(unixSeconds)
        .UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
