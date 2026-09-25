using System.Diagnostics;
using GhostUserRunner.Core.Actions;
using GhostUserRunner.Core.Execution;
using GhostUserRunner.Infrastructure.Paths;
using GhostUserRunner.Infrastructure.Desktop;

namespace GhostUserRunner.Infrastructure.Explorer;

public interface IProcessLauncher { void Start(string fileName, string argument); }

public sealed class ShellProcessLauncher : IProcessLauncher
{
    public void Start(string fileName, string argument) => Process.Start(new ProcessStartInfo(fileName, argument) { UseShellExecute = true });
}

public sealed class FileExplorerAdapter : IActionExecutor
{
    private readonly string[] _roots;
    private readonly HashSet<string> _extensions;
    private readonly string _viewer;
    private readonly IProcessLauncher _launcher;
    private readonly IWindowFocusCoordinator? _focus;
    private string? _currentPath;

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

    public Task<ActionOutcome> ExecuteAsync(ProposedAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var target = CanonicalPathResolver.Resolve(action.Target);
            if (!_roots.Any(root => CanonicalPathResolver.IsBeneath(target, root))) return Task.FromResult(new ActionOutcome(false, "explorer.outside_root"));
            if (action.Kind == ActionKind.BrowseFolder && Directory.Exists(target))
            {
                _launcher.Start("explorer.exe", target);
                if (_focus is not null && !_focus.TryActivate(WindowTarget.Explorer)) return Task.FromResult(new ActionOutcome(false, "explorer.focus_failed"));
                _currentPath = target;
                return Task.FromResult(new ActionOutcome(true, "explorer.folder_opened"));
            }
            if (action.Kind == ActionKind.OpenFile && File.Exists(target) && _extensions.Contains(Path.GetExtension(target)))
            {
                _launcher.Start(_viewer, target);
                if (_focus is not null && !_focus.TryActivate(WindowTarget.Viewer)) return Task.FromResult(new ActionOutcome(false, "explorer.focus_failed"));
                _currentPath = target;
                return Task.FromResult(new ActionOutcome(true, "explorer.file_opened"));
            }
            return Task.FromResult(new ActionOutcome(false, "explorer.action_denied"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return Task.FromResult(new ActionOutcome(false, "explorer.path_denied"));
        }
    }
}
