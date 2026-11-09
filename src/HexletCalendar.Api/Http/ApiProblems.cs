using HexletCalendar.Contracts.Generated;

namespace HexletCalendar.Api.Http;

internal static class ApiProblems
{
    internal static IResult Invalid(string field, string message) => Validation(
        new Dictionary<string, ICollection<string>> { [field] = new[] { message } });

    internal static IResult Validation(IDictionary<string, ICollection<string>> errors) => Results.Json(
        new ValidationProblem
        {
            Type = "urn:hexlet-calendar:problem:validation_error",
            Title = "Ошибка проверки данных",
            Status = StatusCodes.Status400BadRequest,
            Code = "validation_error",
            Errors = errors,
        }, statusCode: StatusCodes.Status400BadRequest, contentType: "application/problem+json");
    internal static IResult Internal() => Results.Json(new Problem
    {
        Type = "urn:hexlet-calendar:problem:internal_error",
        Title = "Внутренняя ошибка сервера",
        Status = StatusCodes.Status500InternalServerError,
        Code = "internal_error",
    }, statusCode: StatusCodes.Status500InternalServerError, contentType: "application/problem+json");

    internal static IResult Problem(int status, string code, string title) => Results.Json(new Problem
    {
        Type = $"urn:hexlet-calendar:problem:{code}",
        Title = title,
        Status = status,
        Code = code,
    }, statusCode: status, contentType: "application/problem+json");
}
