namespace HexletCalendar.Api.Tests;

internal static class RepositoryPaths
{
    internal static string Root
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, "HexletCalendar.slnx")))
                {
                    return directory.FullName;
                }
            }
            throw new InvalidOperationException("Repository root was not found.");
        }
    }
}
