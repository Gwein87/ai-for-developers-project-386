using System.Text;
using NJsonSchema.CodeGeneration.CSharp;
using NSwag;
using NSwag.CodeGeneration.CSharp;

if (args.Length != 2)
{
    throw new ArgumentException("Expected OpenAPI input and C# output paths.");
}
var document = await OpenApiDocument.FromFileAsync(args[0]).ConfigureAwait(false);
var settings = new CSharpClientGeneratorSettings
{
    GenerateClientClasses = false,
    GenerateClientInterfaces = false,
    GenerateExceptionClasses = false,
    GenerateDtoTypes = true,
    CSharpGeneratorSettings =
    {
        Namespace = "HexletCalendar.Contracts.Generated",
        JsonLibrary = CSharpJsonLibrary.SystemTextJson,
        DateTimeType = "string",
        GenerateDataAnnotations = true,
        GenerateNullableReferenceTypes = true,
        GenerateOptionalPropertiesAsNullable = true,
    },
};
var lines = new CSharpClientGenerator(document, settings).GenerateFile()
    .Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
    .Where(line => !line.StartsWith("#pragma warning ", StringComparison.Ordinal))
    .Select(line => line.TrimEnd());
var source = string.Join("\n", lines).TrimEnd() + "\n";
await File.WriteAllTextAsync(args[1], source, new UTF8Encoding(false)).ConfigureAwait(false);
