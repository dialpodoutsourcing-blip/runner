using GhostUserRunner.Core.Actions;
using GhostUserRunner.Core.Execution;

namespace GhostUserRunner.Infrastructure.Execution;

public sealed class CompositeActionExecutor(IActionExecutor browser, IActionExecutor explorer) : IActionExecutor
{
    public ObservedContext Observe() => browser.Observe();

    public Task<ActionOutcome> ExecuteAsync(ProposedAction action, CancellationToken cancellationToken) =>
        action.Kind is ActionKind.BrowseFolder or ActionKind.OpenFile
            ? explorer.ExecuteAsync(action, cancellationToken)
            : browser.ExecuteAsync(action, cancellationToken);
}
