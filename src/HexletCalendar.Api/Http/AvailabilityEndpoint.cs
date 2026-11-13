using System.Globalization;
using HexletCalendar.Contracts.Generated;
using HexletCalendar.Data;
using Microsoft.EntityFrameworkCore;

namespace HexletCalendar.Api.Http;

internal static class AvailabilityEndpoint
{
    internal static async Task<IResult> GetAsync(string id, CalendarDbContext database, TimeProvider clock, CancellationToken cancellation)
    {
        if (!int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var eventTypeId) || eventTypeId <= 0)
        {
            return ApiProblems.Invalid("id", "Укажите положительный целочисленный идентификатор типа события");
        }
        var eventType = await database.EventTypes.AsNoTracking().SingleOrDefaultAsync(item => item.Id == eventTypeId, cancellation).ConfigureAwait(false);
        if (eventType is null)
        {
            return Results.Json(new Problem
            {
                Type = "urn:hexlet-calendar:problem:event_type_not_found",
                Title = "Тип события не найден",
                Status = StatusCodes.Status404NotFound,
                Code = "event_type_not_found",
            }, statusCode: StatusCodes.Status404NotFound, contentType: "application/problem+json");
        }
        var now = clock.GetUtcNow();
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var windowStart = new DateTimeOffset(today.ToDateTime(new TimeOnly(9, 0)), zone.GetUtcOffset(today.ToDateTime(TimeOnly.MinValue))).ToUnixTimeSeconds();
        var windowEnd = new DateTimeOffset(today.AddDays(13).ToDateTime(new TimeOnly(18, 0)), zone.GetUtcOffset(today.AddDays(13).ToDateTime(TimeOnly.MinValue))).ToUnixTimeSeconds();
        var busy = await database.Bookings.AsNoTracking()
            .Where(item => item.StartAt < windowEnd && item.EndAt > windowStart)
            .Select(item => new { item.StartAt, item.EndAt }).ToListAsync(cancellation).ConfigureAwait(false);
        var days = new List<AvailabilityDay>();
        for (var offset = 0; offset < 14; offset++)
        {
            var date = today.AddDays(offset);
            var morning = new DateTimeOffset(date.ToDateTime(new TimeOnly(9, 0)), zone.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue)));
            var slots = new List<AvailabilitySlot>();
            for (var minute = 0; minute + eventType.DurationMinutes <= 540; minute += 15)
            {
                var start = morning.AddMinutes(minute);
                var end = start.AddMinutes(eventType.DurationMinutes);
                slots.Add(new AvailabilitySlot
                {
                    StartAt = BookingDtoMapper.FormatUtc(start),
                    EndAt = BookingDtoMapper.FormatUtc(end),
                    IsAvailable = start > now && !busy.Any(item => item.StartAt < end.ToUnixTimeSeconds() && item.EndAt > start.ToUnixTimeSeconds()),
                });
            }
            days.Add(new AvailabilityDay { Date = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Slots = slots });
        }
        return Results.Ok(new Availability
        {
            EventTypeId = eventTypeId,
            TimeZone = "Europe/Moscow",
            ServerNow = BookingDtoMapper.FormatUtc(now),
            WindowStartDate = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            WindowEndDate = today.AddDays(13).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Days = days,
        });
    }

}
