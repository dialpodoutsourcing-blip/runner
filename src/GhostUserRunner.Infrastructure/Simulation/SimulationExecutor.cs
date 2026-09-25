using GhostUserRunner.Core.Actions;
using GhostUserRunner.Core.Execution;

namespace GhostUserRunner.Infrastructure.Simulation;

public sealed class SimulationExecutor(ObservedContext context) : IActionExecutor
{
    public int ExecutedCount { get; private set; }
    public ObservedContext Observe() => context;
    public Task<ActionOutcome> ExecuteAsync(ProposedAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ExecutedCount++;
        return Task.FromResult(new ActionOutcome(true, "simulated"));
    }
}
