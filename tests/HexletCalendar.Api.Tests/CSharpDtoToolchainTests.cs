using System.Diagnostics;
using System.Text;
using NJsonSchema;
using NJsonSchema.CodeGeneration.CSharp;
using NSwag;
using NSwag.CodeGeneration.CSharp;

namespace HexletCalendar.Api.Tests;

public sealed class CSharpDtoToolchainTests
{
    [Fact]
    public async Task GeneratedDtosAreReproducibleCompileAndPreserveJsonContract()
    {
        var root = FindRepositoryRoot();
        var artifacts = Path.Combine(root, "src", "HexletCalendar.Contracts", "generated");
        var document = await OpenApiDocument.FromFileAsync(Path.Combine(artifacts, "openapi.json"));
        var source = GenerateDtos(document);
        Assert.Equal(source, GenerateDtos(document));
        var artifact = Path.Combine(artifacts, "ProbeDtos.g.cs");
        if (Environment.GetEnvironmentVariable("HEXLET_UPDATE_PROBE_DTOS") == "1")
        {
            await File.WriteAllTextAsync(artifact, source, new UTF8Encoding(false));
        }
        Assert.Equal(source, await File.ReadAllTextAsync(artifact));

        var temporary = Directory.CreateTempSubdirectory("hexlet-csharp-").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(temporary, "ProbeDtos.g.cs"), source);
            await File.WriteAllTextAsync(
                Path.Combine(temporary, "HexletCalendar.DtoSerializationProbe.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>Exe</OutputType>
                    <TargetFramework>net10.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                  </PropertyGroup>
                </Project>
                """);
            await File.WriteAllTextAsync(Path.Combine(temporary, "Program.cs"),
                """
                using System.ComponentModel.DataAnnotations;
                using System.Text.Json;
                using System.Text.Json.Serialization;
                using HexletCalendar.ContractProbe.Generated;

                var item = new ProbeItem
                {
                    Id = 7,
                    CreatedAt = "2026-11-08T09:00:00Z",
                    Detail = new ProbeDetail { Note = null }
                };
                var options = new JsonSerializerOptions
                {
                    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
                };
                var json = JsonSerializer.Serialize(item, options);
                var restored = JsonSerializer.Deserialize<ProbeItem>(json, options)
                    ?? throw new InvalidOperationException("DTO deserialization failed");
                if (restored.Id != 7 || restored.Detail.Note != null)
                    throw new InvalidOperationException("DTO round-trip changed values");
                try
                {
                    JsonSerializer.Deserialize<ProbeItem>(json.Insert(1, "\\\"unexpected\\\":1,"), options);
                    throw new InvalidOperationException("Unknown JSON field was accepted");
                }
                catch (JsonException) { }
                var results = new List<ValidationResult>();
                if (!Validator.TryValidateObject(item, new ValidationContext(item), results, true))
                    throw new InvalidOperationException("Valid DTO was rejected");
                item.Id = 0;
                if (Validator.TryValidateObject(item, new ValidationContext(item), results, true))
                    throw new InvalidOperationException("Generated range was not enforced");
                item.Id = 7;
                item.CreatedAt = "2026-11-08T09:00:00";
                if (Validator.TryValidateObject(item, new ValidationContext(item), results, true))
                    throw new InvalidOperationException("Generated timestamp pattern was not enforced");
                Console.Write(json);
                """);

            await RunDotnetAsync(temporary, "build",
                "HexletCalendar.DtoSerializationProbe.csproj", "--nologo", "-o", "output");
            var json = await RunDotnetAsync(temporary,
                Path.Combine("output", "HexletCalendar.DtoSerializationProbe.dll"));
            Assert.Empty(document.Components.Schemas["ProbeItem"].Validate(json, SchemaType.OpenApi3));
        }
        finally
        {
            Assert.Equal(
                Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar),
                Directory.GetParent(Path.GetFullPath(temporary))?.FullName);
            Assert.StartsWith("hexlet-csharp-", Path.GetFileName(temporary), StringComparison.Ordinal);
            Directory.Delete(temporary, recursive: true);
        }
    }

    private static string GenerateDtos(OpenApiDocument document)
    {
        var settings = new CSharpClientGeneratorSettings
        {
            GenerateClientClasses = false,
            GenerateClientInterfaces = false,
            GenerateDtoTypes = true,
            GenerateExceptionClasses = false,
            CSharpGeneratorSettings =
            {
                Namespace = "HexletCalendar.ContractProbe.Generated",
                JsonLibrary = CSharpJsonLibrary.SystemTextJson,
                DateTimeType = "string",
                GenerateDataAnnotations = true,
                GenerateNullableReferenceTypes = true,
                GenerateOptionalPropertiesAsNullable = true,
            },
        };
        var lines = new CSharpClientGenerator(document, settings).GenerateFile()
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Where(line => !line.StartsWith("#pragma warning ", StringComparison.Ordinal))
            .Select(line => line.TrimEnd());
        return string.Join("\n", lines).TrimEnd() + "\n";
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HexletCalendar.slnx")))
            {
                return directory.FullName;
            }
        }
        throw new InvalidOperationException("Probe repository root was not found");
    }

    private static async Task<string> RunDotnetAsync(string directory, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().ConfigureAwait(false);
            throw;
        }
        var output = await stdout.ConfigureAwait(false);
        var error = await stderr.ConfigureAwait(false);
        Assert.True(process.ExitCode == 0, output + error);
        return output.Trim();
    }
}
