using System.Text.Json;
using System.Text.Json.Serialization;
using HexletCalendar.Api.Http;
using HexletCalendar.Data;
using HexletCalendar.Contracts.Generated;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped(_ => new CalendarDbContext(
    new DbContextOptionsBuilder<CalendarDbContext>()
        .UseSqlite(builder.Configuration.GetConnectionString("Calendar"))
        .Options));
builder.Services.AddHealthChecks();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();

var requestJson = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    PropertyNameCaseInsensitive = false,
    NumberHandling = JsonNumberHandling.Strict,
};
var app = builder.Build();
app.UseExceptionHandler(errors => errors.Run(context => ApiProblems.Internal().ExecuteAsync(context)));
if (app.Environment.IsDevelopment())
{
    app.MapGet("/openapi/v1.json", () => Results.File(
        Path.Combine(AppContext.BaseDirectory, "Contracts", "openapi.json"), "application/json"));
}
app.MapHealthChecks("/api/health");
app.MapGet("/api/event-types/{id}/availability", AvailabilityEndpoint.GetAsync);
app.MapGet("/api/event-types", async (CalendarDbContext database, CancellationToken cancellation) =>
{
    var items = await database.EventTypes.OrderBy(item => item.Id)
        .Select(item => new EventType
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            DurationMinutes = item.DurationMinutes,
        }).ToListAsync(cancellation).ConfigureAwait(false);
    return Results.Ok(new EventTypes { Items = items });
});
app.MapPost("/api/event-types", async (HttpRequest httpRequest, CalendarDbContext database, CancellationToken cancellation) =>
{
    if (!httpRequest.HasJsonContentType())
    {
        return ApiProblems.Invalid("$", "Ожидается объект JSON");
    }
    CreateEventType? request;
    try
    {
        request = await httpRequest.ReadFromJsonAsync<CreateEventType>(requestJson, cancellation).ConfigureAwait(false);
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
    var name = request.Name?.Trim() ?? string.Empty;
    var description = request.Description?.Trim() ?? string.Empty;
    var errors = new Dictionary<string, ICollection<string>>();
    if (name.Length is < 1 or > 100)
    {
        errors["name"] = new List<string> { "Введите название от 1 до 100 символов" };
    }
    if (description.Length is < 1 or > 2000)
    {
        errors["description"] = new List<string> { "Введите описание от 1 до 2000 символов" };
    }
    if (request.DurationMinutes is < 15 or > 540 || request.DurationMinutes % 15 != 0)
    {
        errors["durationMinutes"] = new List<string> { "Укажите целое число от 15 до 540, кратное 15" };
    }
    if (errors.Count > 0)
    {
        return ApiProblems.Validation(errors);
    }
    var item = new EventTypeEntity
    {
        Name = name,
        Description = description,
        DurationMinutes = request.DurationMinutes,
    };
    database.EventTypes.Add(item);
    await database.SaveChangesAsync(cancellation).ConfigureAwait(false);
    return Results.Json(new EventType
    {
        Id = item.Id,
        Name = item.Name,
        Description = item.Description,
        DurationMinutes = item.DurationMinutes,
    }, statusCode: StatusCodes.Status201Created);
});
app.Run();

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Maintainability",
    "CA1515:Consider making public types internal",
    Justification = "WebApplicationFactory requires a public entry-point type for API smoke tests.")]
public partial class Program { }
