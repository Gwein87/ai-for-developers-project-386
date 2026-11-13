using System.Globalization;
using HexletCalendar.Contracts.Generated;
using HexletCalendar.Data;

namespace HexletCalendar.Api.Http;

internal static class BookingDtoMapper
{
    internal static Booking Map(BookingEntity item) => new()
    {
        Id = item.Id,
        EventTypeId = item.EventTypeId,
        EventTypeName = item.EventTypeName,
        StartAt = FormatUtc(item.StartAt),
        EndAt = FormatUtc(item.EndAt),
        GuestName = item.GuestName,
        GuestEmail = item.GuestEmail,
    };

    internal static string FormatUtc(long unixSeconds) => FormatUtc(DateTimeOffset.FromUnixTimeSeconds(unixSeconds));

    internal static string FormatUtc(DateTimeOffset timestamp) => timestamp.UtcDateTime
        .ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
