using System.Diagnostics;
using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;
using HexletCalendar.Contracts.Generated;
using HexletCalendar.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HexletCalendar.Api.Http;

internal static class BookingEndpoint
{
    private const int MaximumAttempts = 3;
    private static readonly TimeSpan RetryBudget = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);
    private static readonly JsonSerializerOptions RequestJson = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict,
    };

    internal static async Task<IResult> PostAsync(
        HttpRequest httpRequest,
        IConfiguration configuration,
        TimeProvider clock,
        CancellationToken cancellation)
    {
        if (!httpRequest.HasJsonContentType())
        {
            return ApiProblems.Invalid("$", "Ожидается объект JSON");
        }
        if (!httpRequest.Headers.TryGetValue("Idempotency-Key", out var header)
            || header.Count != 1
            || !Guid.TryParseExact(header[0], "D", out var key))
        {
            return ApiProblems.Invalid("idempotencyKey", "Укажите UUID в заголовке Idempotency-Key");
        }
        CreateBooking? request;
        try
        {
            request = await httpRequest.ReadFromJsonAsync<CreateBooking>(RequestJson, cancellation).ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            var field = exception.Path?.StartsWith("$.", StringComparison.Ordinal) == true ? exception.Path[2..] : "$";
            return ApiProblems.Invalid(field, "Неверный JSON или тип поля");
        }
        if (request is null)
        {
            return ApiProblems.Invalid("$", "Ожидается объект JSON");
        }

        var guestName = request.GuestName?.Trim() ?? string.Empty;
        var guestEmail = request.GuestEmail?.Trim() ?? string.Empty;
        var errors = Validate(request, guestName, guestEmail, out var start);
        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        var connectionString = configuration.GetConnectionString("Calendar")
            ?? throw new InvalidOperationException("Calendar connection string is not configured.");
        var stopwatch = Stopwatch.StartNew();
        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            try
            {
                return await TryCreateAsync(connectionString, key.ToString("D"), request.EventTypeId, start, guestName, guestEmail, clock, cancellation)
                    .ConfigureAwait(false);
            }
            catch (SqliteException exception) when (IsLocked(exception) && attempt < MaximumAttempts && stopwatch.Elapsed + RetryDelay < RetryBudget)
            {
                await Task.Delay(RetryDelay, cancellation).ConfigureAwait(false);
            }
            catch (SqliteException exception) when (IsLocked(exception))
            {
                return ApiProblems.Problem(StatusCodes.Status503ServiceUnavailable, "temporarily_unavailable", "Сервис временно недоступен");
            }
        }
        return ApiProblems.Problem(StatusCodes.Status503ServiceUnavailable, "temporarily_unavailable", "Сервис временно недоступен");
    }

    private static Dictionary<string, ICollection<string>> Validate(
        CreateBooking request,
        string guestName,
        string guestEmail,
        out DateTimeOffset start)
    {
        var errors = new Dictionary<string, ICollection<string>>();
        if (request.EventTypeId <= 0)
        {
            errors["eventTypeId"] = ["Укажите положительный идентификатор типа события"];
        }
        if (!DateTimeOffset.TryParseExact(request.StartAt, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out start)
            || start.Second != 0
            || !string.Equals(request.StartAt, start.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            errors["startAt"] = ["Укажите время в формате UTC с точностью до минуты"];
        }
        if (guestName.Length is < 1 or > 100)
        {
            errors["guestName"] = ["Введите имя от 1 до 100 символов"];
        }
        if (guestEmail.Length is < 1 or > 254 || !IsEmail(guestEmail))
        {
            errors["guestEmail"] = ["Введите корректный email длиной до 254 символов"];
        }
        return errors;
    }

    private static bool IsEmail(string value)
    {
        try
        {
            var address = new MailAddress(value);
            return string.Equals(address.Address, value, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static async Task<IResult> TryCreateAsync(
        string connectionString,
        string key,
        int eventTypeId,
        DateTimeOffset start,
        string guestName,
        string guestEmail,
        TimeProvider clock,
        CancellationToken cancellation)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString) { DefaultTimeout = 1, Pooling = false };
        using var connection = new SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellation).ConfigureAwait(false);
        using var transaction = BeginImmediate(connection);
        using var database = new CalendarDbContext(new DbContextOptionsBuilder<CalendarDbContext>().UseSqlite(connection).Options);
        Enlist(database, transaction);

        var existing = await database.Bookings.AsNoTracking().SingleOrDefaultAsync(item => item.IdempotencyKey == key, cancellation).ConfigureAwait(false);
        if (existing is not null)
        {
            var same = existing.EventTypeId == eventTypeId
                && existing.StartAt == start.ToUnixTimeSeconds()
                && existing.GuestName == guestName
                && existing.GuestEmail == guestEmail;
            await transaction.CommitAsync(cancellation).ConfigureAwait(false);
            return same
                ? Created(existing)
                : ApiProblems.Problem(StatusCodes.Status409Conflict, "idempotency_key_reused", "Ключ попытки уже использован с другими данными");
        }

        var eventType = await database.EventTypes.AsNoTracking().SingleOrDefaultAsync(item => item.Id == eventTypeId, cancellation).ConfigureAwait(false);
        if (eventType is null)
        {
            return ApiProblems.Problem(StatusCodes.Status404NotFound, "event_type_not_found", "Тип события не найден");
        }
        var end = start.AddMinutes(eventType.DurationMinutes);
        if (!IsValidSlot(start, end, clock.GetUtcNow()))
        {
            return ApiProblems.Problem(StatusCodes.Status400BadRequest, "invalid_slot", "Выбранное время больше недоступно");
        }
        var startSeconds = start.ToUnixTimeSeconds();
        var endSeconds = end.ToUnixTimeSeconds();
        var occupied = await database.Bookings.AsNoTracking()
            .AnyAsync(item => item.StartAt < endSeconds && item.EndAt > startSeconds, cancellation).ConfigureAwait(false);
        if (occupied)
        {
            return ApiProblems.Problem(StatusCodes.Status409Conflict, "slot_taken", "Выбранное время уже занято");
        }
        var booking = new BookingEntity
        {
            EventTypeId = eventType.Id,
            EventTypeName = eventType.Name,
            StartAt = startSeconds,
            EndAt = endSeconds,
            GuestName = guestName,
            GuestEmail = guestEmail,
            IdempotencyKey = key,
        };
        database.Bookings.Add(booking);
        await database.SaveChangesAsync(cancellation).ConfigureAwait(false);
        await transaction.CommitAsync(cancellation).ConfigureAwait(false);
        return Created(booking);
    }

    private static bool IsValidSlot(DateTimeOffset start, DateTimeOffset end, DateTimeOffset now)
    {
        if (start <= now || start.Second != 0 || start.Millisecond != 0)
        {
            return false;
        }
        var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
        var localStart = TimeZoneInfo.ConvertTime(start, zone);
        var localEnd = TimeZoneInfo.ConvertTime(end, zone);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var date = DateOnly.FromDateTime(localStart.DateTime);
        return date >= today
            && date <= today.AddDays(13)
            && localStart.Minute % 15 == 0
            && localStart.Hour >= 9
            && localEnd.Date == localStart.Date
            && localEnd.TimeOfDay <= new TimeSpan(18, 0, 0);
    }

    private static IResult Created(BookingEntity item) => Results.Json(BookingDtoMapper.Map(item), statusCode: StatusCodes.Status201Created);

    private static bool IsLocked(SqliteException exception) => exception.SqliteErrorCode is 5 or 6;

    private static SqliteTransaction BeginImmediate(SqliteConnection connection) => connection.BeginTransaction(deferred: false);

    private static void Enlist(CalendarDbContext database, SqliteTransaction transaction) => database.Database.UseTransaction(transaction);
}
