using GhostUserRunner.Core.Execution;
using GhostUserRunner.Core.Logging;
using GhostUserRunner.Core.Planning;
using GhostUserRunner.Core.Safety;

namespace GhostUserRunner.Core.Session;

public sealed class SessionController(IActivityPlanner planner, IActionPolicy policy, IActionExecutor executor)
{
    private readonly SemaphoreSlim _transition = new(1, 1);
    private readonly object _stateLock = new();
    private readonly List<GeneratedActionEvent> _events = [];
    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private CancellationTokenSource? _actionCancellation;
    private Task<ActionOutcome>? _currentExecution;
    private volatile bool _paused;
    private Guid? _sessionId;
    private DateTimeOffset? _startedAt;
    private TimeSpan? _duration;
    private string? _currentActivity;
    private string? _error;

    public SessionState State { get; private set; } = SessionState.Stopped;
    public IReadOnlyList<GeneratedActionEvent> Events => _events;

    public SessionStatus GetStatus(DateTimeOffset now)
    {
        var elapsed = _startedAt is { } started ? now - started : TimeSpan.Zero;
        TimeSpan? remaining = _duration is { } duration ? TimeSpan.Zero.Max(duration - elapsed) : null;
        lock (_events)
            return new(State, _sessionId, _startedAt, elapsed, remaining, _currentActivity,
                _events.LastOrDefault(), _events.TakeLast(100).ToArray(), _error);
    }

    public async Task RunAsync(SessionRequest request, CancellationToken cancellationToken)
    {
        await _transition.WaitAsync(cancellationToken);
        try
        {
            if (State is not SessionState.Stopped) throw new InvalidOperationException("A session is already active.");
            _runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _runCancellation.CancelAfter(request.Duration);
            State = SessionState.Starting;
            _sessionId = Guid.NewGuid();
            _startedAt = DateTimeOffset.UtcNow;
            _duration = request.Duration;
            _error = null;
            State = SessionState.Running;
            _runTask = RunLoopAsync(request, _runCancellation.Token);
        }
        finally { _transition.Release(); }

        await _runTask;
    }

    public async Task PauseAsync()
    {
        Task? execution;
        lock (_stateLock)
        {
            if (State != SessionState.Running) throw new InvalidOperationException("Only a running session can be paused.");
            _paused = true;
            State = SessionState.Pausing;
            execution = _currentExecution;
            try { _actionCancellation?.Cancel(); } catch (ObjectDisposedException) { }
        }
        if (execution is not null) try { await execution; } catch (OperationCanceledException) { }
        lock (_stateLock) if (State == SessionState.Pausing) State = SessionState.Paused;
    }

    public void Resume()
    {
        lock (_stateLock)
        {
            if (State != SessionState.Paused) throw new InvalidOperationException("Only a paused session can be resumed.");
            _paused = false;
            State = SessionState.Running;
        }
    }

    public async Task StopAsync()
    {
        Task? run;
        lock (_stateLock)
        {
            if (State is SessionState.Stopped or SessionState.Failed) return;
            State = SessionState.Stopping;
            _paused = false;
            _runCancellation?.Cancel();
            try { _actionCancellation?.Cancel(); } catch (ObjectDisposedException) { }
            run = _runTask;
        }
        if (run is not null) await run;
    }

    private async Task RunLoopAsync(SessionRequest request, CancellationToken cancellationToken)
    {
        var sessionId = _sessionId ?? Guid.NewGuid();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (_paused) await Task.Delay(25, cancellationToken);
                var context = executor.Observe();
                var action = planner.Next(context);
                _currentActivity = action.Kind.ToString();
                var decision = policy.Evaluate(action, context);
                if (!decision.Allowed)
                {
                    lock (_events) _events.Add(new(DateTimeOffset.UtcNow, sessionId, request.Seed, action.Kind, action.Target, decision.ReasonCode));
                    State = SessionState.Failed;
                    return;
                }
                using var actionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task<ActionOutcome> execution;
                lock (_stateLock)
                {
                    _actionCancellation = actionCancellation;
                    execution = executor.ExecuteAsync(action, actionCancellation.Token);
                    _currentExecution = execution;
                }
                ActionOutcome outcome;
                try { outcome = await execution; }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && _paused) { continue; }
                finally
                {
                    lock (_stateLock)
                    {
                        if (ReferenceEquals(_actionCancellation, actionCancellation)) _actionCancellation = null;
                        if (ReferenceEquals(_currentExecution, execution)) _currentExecution = null;
                    }
                }
                planner.Record(outcome);
                lock (_events) _events.Add(new(DateTimeOffset.UtcNow, sessionId, request.Seed, action.Kind, action.Target, outcome.Code));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            lock (_events) _events.Add(new(DateTimeOffset.UtcNow, sessionId, request.Seed, Actions.ActionKind.Idle, "session", exception.GetType().Name));
            _error = exception.Message;
            State = SessionState.Failed;
            return;
        }
        finally
        {
            _currentActivity = null;
            if (State != SessionState.Failed) State = SessionState.Stopped;
        }
    }
}

file static class TimeSpanExtensions
{
    public static TimeSpan Max(this TimeSpan left, TimeSpan right) => left > right ? left : right;
}
