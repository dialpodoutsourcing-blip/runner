using GhostUserRunner.Core.Actions;
using GhostUserRunner.Core.Execution;
using GhostUserRunner.Core.Planning;
using GhostUserRunner.Core.Safety;
using GhostUserRunner.Core.Session;

namespace GhostUserRunner.Core.Tests.Session;

public sealed class SessionControllerTests
{
    [Fact]
    public void NewControllerReportsStoppedStatus()
    {
        var status = CreateController(new CountingExecutor()).GetStatus(DateTimeOffset.UtcNow);
        Assert.Equal(SessionState.Stopped, status.State);
        Assert.Null(status.SessionId);
    }
    [Fact]
    public async Task StopCancelsInFlightExecutionAndReturnsToStopped()
    {
        var executor = new BlockingExecutor();
        var controller = CreateController(executor);
        var running = controller.RunAsync(new SessionRequest(TimeSpan.FromHours(8), 1), CancellationToken.None);
        await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await controller.StopAsync();
        await running;

        Assert.True(executor.WasCancelled);
        Assert.Equal(SessionState.Stopped, controller.State);
    }

    [Fact]
    public async Task PauseAndResumeChangeState()
    {
        var executor = new BlockingExecutor();
        var controller = CreateController(executor);
        var running = controller.RunAsync(new SessionRequest(TimeSpan.FromHours(8), 2), CancellationToken.None);
        await executor.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await controller.PauseAsync();
        Assert.True(executor.WasCancelled);
        Assert.Equal(SessionState.Paused, controller.State);
        controller.Resume();
        Assert.Equal(SessionState.Running, controller.State);
        await controller.StopAsync();
        await running;
    }

    [Fact]
    public async Task PolicyDenialFailsSessionWithoutExecuting()
    {
        var executor = new CountingExecutor();
        var controller = CreateController(executor, new DenyPolicy());

        await controller.RunAsync(new SessionRequest(TimeSpan.FromSeconds(1), 3), CancellationToken.None);

        Assert.Equal(0, executor.Count);
        Assert.Equal(SessionState.Failed, controller.State);
    }

    [Fact]
    public async Task WaitsTenSecondsBetweenCompletedActions()
    {
        var delay = new BlockingDelay();
        var controller = new SessionController(new FixedPlanner(), new PermitPolicy(), new CountingExecutor(), delay);
        var running = controller.RunAsync(new SessionRequest(TimeSpan.FromHours(8), 4), CancellationToken.None);

        var requestedDelay = await delay.Requested.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await controller.StopAsync();
        await running;

        Assert.Equal(TimeSpan.FromSeconds(10), requestedDelay);
    }

    private static SessionController CreateController(IActionExecutor executor, IActionPolicy? policy = null) =>
        new(new FixedPlanner(), policy ?? new PermitPolicy(), executor);

    private sealed class FixedPlanner : IActivityPlanner
    {
        public ProposedAction Next(ObservedContext context) => new(ActionKind.Idle, "idle");
        public void Record(ActionOutcome outcome) { }
    }

    private sealed class PermitPolicy : IActionPolicy
    {
        public PolicyDecision Evaluate(ProposedAction action, ObservedContext context) => PolicyDecision.Permit();
    }

    private sealed class DenyPolicy : IActionPolicy
    {
        public PolicyDecision Evaluate(ProposedAction action, ObservedContext context) => PolicyDecision.Deny("test.denied");
    }

    private sealed class BlockingExecutor : IActionExecutor
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool WasCancelled { get; private set; }
        public ObservedContext Observe() => new(true, "test", null, null);
        public async Task<ActionOutcome> ExecuteAsync(ProposedAction action, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken); }
            catch (OperationCanceledException) { WasCancelled = true; throw; }
            return new(true, "complete");
        }
    }

    private sealed class CountingExecutor : IActionExecutor
    {
        public int Count { get; private set; }
        public ObservedContext Observe() => new(true, "test", null, null);
        public Task<ActionOutcome> ExecuteAsync(ProposedAction action, CancellationToken cancellationToken)
        { Count++; return Task.FromResult(new ActionOutcome(true, "complete")); }
    }

    private sealed class BlockingDelay : IActionDelay
    {
        public TaskCompletionSource<TimeSpan> Requested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task WaitAsync(TimeSpan duration, CancellationToken cancellationToken)
        {
            Requested.TrySetResult(duration);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
