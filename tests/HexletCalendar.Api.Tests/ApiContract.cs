using System.Globalization;
using System.Text.Json;
using NJsonSchema;
using NSwag;

namespace HexletCalendar.Api.Tests;

internal static class ApiContract
{
    internal static async Task<string[]> ValidateAsync(HttpResponseMessage response, string path, string method)
    {
        var document = await OpenApiDocument.FromFileAsync(Path.Combine(
            RepositoryPaths.Root, "src", "HexletCalendar.Contracts", "generated", "openapi.json")).ConfigureAwait(false);
        var operation = document.Paths[path][method];
        var status = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
        if (!operation.Responses.TryGetValue(status, out var declared))
        {
            return ["Undeclared response status: " + status];
        }
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is null || !declared.Content.TryGetValue(contentType, out var media))
        {
            return ["Undeclared response content type: " + contentType];
        }
        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var json = contentType == "text/plain" ? JsonSerializer.Serialize(body) : body;
        return media.Schema.Validate(json, SchemaType.OpenApi3).Select(error => error.ToString()).ToArray();
    }
}
