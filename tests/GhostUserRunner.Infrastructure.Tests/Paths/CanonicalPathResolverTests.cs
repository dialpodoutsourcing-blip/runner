using GhostUserRunner.Infrastructure.Paths;

namespace GhostUserRunner.Infrastructure.Tests.Paths;

public sealed class CanonicalPathResolverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ghost-runner-tests", Guid.NewGuid().ToString("N"));

    public CanonicalPathResolverTests() => Directory.CreateDirectory(Path.Combine(_root, "safe"));

    [Fact]
    public void ResolvesExistingPathBeneathRoot()
    {
        var file = Path.Combine(_root, "safe", "note.txt");
        File.WriteAllText(file, "safe");

        Assert.True(CanonicalPathResolver.IsBeneath(file, Path.Combine(_root, "safe")));
    }

    [Fact]
    public void RejectsSiblingPrefixAndParentEscape()
    {
        var sibling = Directory.CreateDirectory(Path.Combine(_root, "safe2")).FullName;

        Assert.False(CanonicalPathResolver.IsBeneath(sibling, Path.Combine(_root, "safe")));
        Assert.False(CanonicalPathResolver.IsBeneath(Path.Combine(_root, "safe", "..", "safe2"), Path.Combine(_root, "safe")));
    }

    [Fact]
    public void RejectsMissingAndUncPaths()
    {
        Assert.Throws<FileNotFoundException>(() => CanonicalPathResolver.Resolve(Path.Combine(_root, "missing")));
        Assert.Throws<NotSupportedException>(() => CanonicalPathResolver.Resolve(@"\\server\share\file.txt"));
    }

    public void Dispose() => Directory.Delete(_root, true);
}
