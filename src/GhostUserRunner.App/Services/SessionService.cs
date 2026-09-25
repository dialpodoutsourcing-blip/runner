using System.Security.Cryptography;
using GhostUserRunner.Core.Configuration;
using GhostUserRunner.Core.Planning;
using GhostUserRunner.Core.Randomness;
using GhostUserRunner.Core.Safety;
using GhostUserRunner.Core.Session;
using GhostUserRunner.Infrastructure.Browser;
using GhostUserRunner.Infrastructure.Desktop;
using GhostUserRunner.Infrastructure.Execution;
using GhostUserRunner.Infrastructure.Explorer;
using GhostUserRunner.Infrastructure.Input;
using GhostUserRunner.Infrastructure.Safety;

namespace GhostUserRunner.App.Services;

public sealed record SessionCommandResult(bool Accepted, string Code, string? Message = null);

public sealed class SessionService(RunnerOptions options) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SessionController? _controller;
    private BrowserAdapter? _browser;
    private FileExplorerAdapter? _explorer;
    private CancellationTokenSource? _lifetime;
    private Task? _run;

    public SessionStatus Status => _controller?.GetStatus(DateTimeOffset.UtcNow)
        ?? new(SessionState.Stopped, null, null, TimeSpan.Zero, null, null, null, [], null);

    public async Task<SessionCommandResult> StartAsync(TimeSpan? duration, int? seed)
    {
        await _gate.WaitAsync();
        try
        {
            var actualDuration = duration ?? SessionLimits.DefaultDuration;
            if (actualDuration < SessionLimits.MinimumDuration) return new(false, "session.too_short", "Duration must be at least eight hours.");
            if (_run is { IsCompleted: false }) return new(false, "session.already_active", "A session is already active.");
            var actualSeed = seed ?? RandomNumberGenerator.GetInt32(int.MaxValue);
            var random = new SeededRandomSource(actualSeed);
            var input = new HumanInputEngine(new Win32InputSink(), random);
            var focus = new WindowFocusCoordinator(new Win32WindowApi());
            var profile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GhostUserRunner", "BrowserProfile");
            _browser = new BrowserAdapter(options.AllowedDomains, profile, input, focus);
            _explorer = new FileExplorerAdapter(options.AllowedFolderRoots, options.AllowedExtensions, options.AllowedApplications, new ShellProcessLauncher(), focus);
            var planner = new ActivityPlanner(options, random, new SearchTopicGenerator(["nature documentaries", "space exploration", "world history", "cooking techniques", "classical music", "technology news"]));
            _controller = new SessionController(planner, new ActionPolicy(options), new CompositeActionExecutor(_browser, _explorer));
            _lifetime = new CancellationTokenSource();
            _run = RunOwnedAsync(new(actualDuration, actualSeed), _lifetime.Token);
            return new(true, "session.started");
        }
        finally { _gate.Release(); }
    }

    public async Task<SessionCommandResult> PauseAsync()
    { if (_controller?.State != SessionState.Running) return new(false, "session.not_running"); await _controller.PauseAsync(); return new(true, "session.paused"); }
    public SessionCommandResult Resume()
    { if (_controller?.State != SessionState.Paused) return new(false, "session.not_paused"); _controller.Resume(); return new(true, "session.resumed"); }
    public async Task<SessionCommandResult> StopAsync()
    { if (_controller is null) return new(true, "session.stopped"); await _controller.StopAsync(); _lifetime?.Cancel(); return new(true, "session.stopped"); }

    private async Task RunOwnedAsync(SessionRequest request, CancellationToken token)
    {
        using var monitorCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var monitor = new StopMonitor().MonitorAsync(() => _ = StopAsync(), monitorCancellation.Token);
        try { await _controller!.RunAsync(request, token); }
        finally
        {
            monitorCancellation.Cancel();
            try { await monitor; } catch (OperationCanceledException) { }
            if (_browser is not null) await _browser.DisposeAsync();
            if (_explorer is not null) await _explorer.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    { await StopAsync(); if (_run is not null) try { await _run; } catch (OperationCanceledException) { } _lifetime?.Dispose(); _gate.Dispose(); }
}
