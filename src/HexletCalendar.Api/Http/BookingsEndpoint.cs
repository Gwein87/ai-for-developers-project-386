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
        var items = entities.Select(BookingDtoMapper.Map).ToList();
        return Results.Ok(new Bookings
        {
            TimeZone = "Europe/Moscow",
            ServerNow = BookingDtoMapper.FormatUtc(now),
            Items = items,
        });
    }

}
