using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;


namespace HexletCalendar.Api.Tests;

internal sealed class CalendarApiFactory : WebApplicationFactory<Program>
{
    private readonly string directory = Directory.CreateTempSubdirectory("hexlet-calendar-").FullName;
    private readonly string database;

    internal CalendarApiFactory()
    {
        database = Path.Combine(directory, "calendar.db");
        File.Copy(Path.Combine(RepositoryPaths.Root, "tests", "HexletCalendar.Api.Tests", "Fixtures", "calendar.db"), database);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.UseSetting("ConnectionStrings:Calendar", $"Data Source={database};Mode=ReadWrite;Pooling=False");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(directory))
        {

            var temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar);
            if (Directory.GetParent(Path.GetFullPath(directory))?.FullName != temporaryRoot
                || !Path.GetFileName(directory).StartsWith("hexlet-calendar-", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Unexpected temporary database directory.");
            }
            Directory.Delete(directory, recursive: true);
        }
    }
}
