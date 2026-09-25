using GhostUserRunner.Core.Actions;
using GhostUserRunner.Infrastructure.Explorer;
using GhostUserRunner.Infrastructure.Desktop;

namespace GhostUserRunner.Infrastructure.Tests.Explorer;

public sealed class FileExplorerAdapterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ghost-explorer-tests", Guid.NewGuid().ToString("N"));

    public FileExplorerAdapterTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task OpensApprovedFolderWithExplorer()
    {
        var launcher = new RecordingLauncher();
        var adapter = new FileExplorerAdapter([_root], [".txt"], [@"C:\Windows\System32\notepad.exe"], launcher);

        var outcome = await adapter.ExecuteAsync(new(ActionKind.BrowseFolder, _root), CancellationToken.None);

        Assert.True(outcome.Succeeded);
        Assert.Equal("explorer.exe", launcher.FileName);
        Assert.Equal($"/separate,\"{Path.GetFullPath(_root)}\"", launcher.Argument);
    }

    [Fact]
    public async Task RejectsOutsideFolderAndUnapprovedExtension()
    {
        var outside = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "ghost-outside", Guid.NewGuid().ToString("N"))).FullName;
        var executable = Path.Combine(_root, "unsafe.exe");
        await File.WriteAllTextAsync(executable, "not executable");
        var adapter = new FileExplorerAdapter([_root], [".txt"], [@"C:\Windows\System32\notepad.exe"], new RecordingLauncher());

        Assert.False((await adapter.ExecuteAsync(new(ActionKind.BrowseFolder, outside), CancellationToken.None)).Succeeded);
        Assert.False((await adapter.ExecuteAsync(new(ActionKind.OpenFile, executable), CancellationToken.None)).Succeeded);
        Directory.Delete(outside, true);
    }

    [Fact]
    public async Task OpensApprovedFileOnlyWithConfiguredViewer()
    {
        var file = Path.Combine(_root, "notes.txt");
        await File.WriteAllTextAsync(file, "safe");
        var launcher = new RecordingLauncher();
        var adapter = new FileExplorerAdapter([_root], [".txt"], [@"C:\Windows\System32\notepad.exe"], launcher);

        var result = await adapter.ExecuteAsync(new(ActionKind.OpenFile, file), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(@"C:\Windows\System32\notepad.exe", launcher.FileName);
        Assert.Equal(Path.GetFullPath(file), launcher.Argument);
    }

    [Fact]
    public async Task RefusesInteractionWhenLaunchedWindowCannotBeFocused()
    {
        var launcher = new RecordingLauncher();
        var adapter = new FileExplorerAdapter([_root], [".txt"], [@"C:\Windows\System32\notepad.exe"], launcher, new RejectingFocus());

        var result = await adapter.ExecuteAsync(new(ActionKind.BrowseFolder, _root), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("explorer.focus_failed", result.Code);
        Assert.True(launcher.Process.WasClosed);
    }

    [Fact]
    public async Task DisposalClosesAnOwnedWindow()
    {
        var launcher = new RecordingLauncher();
        var adapter = new FileExplorerAdapter([_root], [".txt"], [@"C:\Windows\System32\notepad.exe"], launcher);
        await adapter.ExecuteAsync(new(ActionKind.BrowseFolder, _root), CancellationToken.None);

        await adapter.DisposeAsync();

        Assert.True(launcher.Process.WasClosed);
    }

    [Fact]
    public async Task ClosesOnlyTheWindowItLaunched()
    {
        var launcher = new RecordingLauncher();
        var adapter = new FileExplorerAdapter([_root], [".txt"], [@"C:\Windows\System32\notepad.exe"], launcher);
        await adapter.ExecuteAsync(new(ActionKind.BrowseFolder, _root), CancellationToken.None);

        var result = await adapter.ExecuteAsync(new(ActionKind.CloseWindow, "owned"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(launcher.Process.WasClosed);
    }

    public void Dispose() => Directory.Delete(_root, true);

    private sealed class RecordingLauncher : IProcessLauncher
    {
        public RecordingProcess Process { get; } = new();
        public string? FileName { get; private set; }
        public string? Argument { get; private set; }
        public ILaunchedProcess Start(string fileName, string argument)
        { FileName = fileName; Argument = argument; return Process; }
    }

    private sealed class RecordingProcess : ILaunchedProcess
    {
        public bool WasClosed { get; private set; }
        public Task CloseAsync(CancellationToken cancellationToken)
        { WasClosed = true; return Task.CompletedTask; }
    }

    private sealed class RejectingFocus : IWindowFocusCoordinator
    {
        public bool TryActivate(WindowTarget target) => false;
    }

}
