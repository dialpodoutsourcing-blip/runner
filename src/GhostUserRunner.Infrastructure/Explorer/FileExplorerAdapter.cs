using System.Diagnostics;
using GhostUserRunner.Core.Actions;
using GhostUserRunner.Core.Execution;
using GhostUserRunner.Infrastructure.Paths;
using GhostUserRunner.Infrastructure.Desktop;

namespace GhostUserRunner.Infrastructure.Explorer;

public interface IProcessLauncher { ILaunchedProcess Start(string fileName, string argument); }
public interface ILaunchedProcess { Task CloseAsync(CancellationToken cancellationToken); }

public sealed class ShellProcessLauncher : IProcessLauncher
{
    public ILaunchedProcess Start(string fileName, string argument) =>
        new OwnedProcess(Process.Start(new ProcessStartInfo(fileName, argument) { UseShellExecute = true })
            ?? throw new InvalidOperationException($"Could not launch '{fileName}'."));

    private sealed class OwnedProcess(Process process) : ILaunchedProcess
    {
        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (process.HasExited) return;
                if (!process.CloseMainWindow()) process.Kill(entireProcessTree: true);
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(2));
                try { await process.WaitForExitAsync(timeout.Token); }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                }
            }
            finally { process.Dispose(); }
        }
    }
}

public sealed class FileExplorerAdapter : IActionExecutor, IAsyncDisposable
{
    private readonly string[] _roots;
    private readonly HashSet<string> _extensions;
    private readonly string _viewer;
    private readonly IProcessLauncher _launcher;
    private readonly IWindowFocusCoordinator? _focus;
    private string? _currentPath;
    private ILaunchedProcess? _ownedProcess;

    public FileExplorerAdapter(IEnumerable<string> roots, IEnumerable<string> extensions, IEnumerable<string> viewerApplications, IProcessLauncher launcher, IWindowFocusCoordinator? focus = null)
    {
        _roots = roots.Select(CanonicalPathResolver.Resolve).ToArray();
        _extensions = new(extensions, StringComparer.OrdinalIgnoreCase);
        _viewer = viewerApplications.Select(Path.GetFullPath).FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException("No configured viewer application exists.");
        _launcher = launcher;
        _focus = focus;
    }

    public ObservedContext Observe() => new(_currentPath is not null, "explorer", null, _currentPath);

    public async Task<ActionOutcome> ExecuteAsync(ProposedAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (action.Kind == ActionKind.CloseWindow) return await CloseOwnedWindowAsync(cancellationToken);
        try
        {
            var target = CanonicalPathResolver.Resolve(action.Target);
            if (!_roots.Any(root => CanonicalPathResolver.IsBeneath(target, root))) return new(false, "explorer.outside_root");
            if (action.Kind == ActionKind.BrowseFolder && Directory.Exists(target))
            {
                _ownedProcess = _launcher.Start("explorer.exe", $"/separate,\"{target}\"");
                if (_focus is not null && !_focus.TryActivate(WindowTarget.Explorer))
                {
                    await CloseOwnedWindowAsync(cancellationToken);
                    return new(false, "explorer.focus_failed");
                }
                _currentPath = target;
                return new(true, "explorer.folder_opened");
            }
            if (action.Kind == ActionKind.OpenFile && File.Exists(target) && _extensions.Contains(Path.GetExtension(target)))
            {
                _ownedProcess = _launcher.Start(_viewer, target);
                if (_focus is not null && !_focus.TryActivate(WindowTarget.Viewer))
                {
                    await CloseOwnedWindowAsync(cancellationToken);
                    return new(false, "explorer.focus_failed");
                }
                _currentPath = target;
                return new(true, "explorer.file_opened");
            }
            return new(false, "explorer.action_denied");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new(false, "explorer.path_denied");
        }
    }

    private async Task<ActionOutcome> CloseOwnedWindowAsync(CancellationToken cancellationToken)
    {
        if (_ownedProcess is null) return new(true, "window.none");
        await _ownedProcess.CloseAsync(cancellationToken);
        _ownedProcess = null;
        _currentPath = null;
        return new(true, "window.closed");
    }

    public async ValueTask DisposeAsync() => await CloseOwnedWindowAsync(CancellationToken.None);
}
