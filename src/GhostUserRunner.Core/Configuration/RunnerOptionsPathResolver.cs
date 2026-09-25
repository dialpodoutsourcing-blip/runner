namespace GhostUserRunner.Core.Configuration;

public static class RunnerOptionsPathResolver
{
    private const string AppDirectoryToken = "{AppDirectory}";

    public static RunnerOptions Resolve(RunnerOptions options, string appDirectory)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(appDirectory);

        var fullAppDirectory = Path.GetFullPath(appDirectory);
        return options with
        {
            AllowedFolderRoots = options.AllowedFolderRoots
                .Select(path => ResolveAppDirectory(path, fullAppDirectory))
                .ToArray()
        };
    }

    private static string ResolveAppDirectory(string path, string appDirectory)
    {
        if (!path.StartsWith(AppDirectoryToken, StringComparison.OrdinalIgnoreCase)) return path;

        var relativePath = path[AppDirectoryToken.Length..]
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(appDirectory, relativePath));
    }
}
