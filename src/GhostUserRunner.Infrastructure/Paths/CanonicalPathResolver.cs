namespace GhostUserRunner.Infrastructure.Paths;

public static class CanonicalPathResolver
{
    public static string Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));
        if (path.StartsWith(@"\\", StringComparison.Ordinal)) throw new NotSupportedException("Network and device paths are not supported.");

        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
        {
            throw new FileNotFoundException("The configured path does not exist.", fullPath);
        }

        return ResolveLink(fullPath);
    }

    public static bool IsBeneath(string candidate, string root)
    {
        var candidatePath = Resolve(candidate).TrimEnd(Path.DirectorySeparatorChar);
        var rootPath = Resolve(root).TrimEnd(Path.DirectorySeparatorChar);
        return candidatePath.Equals(rootPath, StringComparison.OrdinalIgnoreCase) ||
            candidatePath.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveLink(string path)
    {
        FileSystemInfo info = Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path);
        var target = info.ResolveLinkTarget(true);
        return Path.GetFullPath(target?.FullName ?? info.FullName);
    }
}
