using GhostUserRunner.Core.Actions;

namespace GhostUserRunner.Core.Execution;

public sealed record ActionOutcome(bool Succeeded, string Code, string? Detail = null);

public interface IActionExecutor
{
    ObservedContext Observe();
    Task<ActionOutcome> ExecuteAsync(ProposedAction action, CancellationToken cancellationToken);
}
