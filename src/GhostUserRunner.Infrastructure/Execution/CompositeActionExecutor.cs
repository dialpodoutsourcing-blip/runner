using GhostUserRunner.Core.Actions;
using GhostUserRunner.Core.Execution;

namespace GhostUserRunner.Infrastructure.Execution;

public sealed class CompositeActionExecutor(IActionExecutor browser, IActionExecutor explorer) : IActionExecutor
{
    private WindowOwner _owner;

    public ObservedContext Observe() => _owner == WindowOwner.Explorer ? explorer.Observe() : browser.Observe();

    public async Task<ActionOutcome> ExecuteAsync(ProposedAction action, CancellationToken cancellationToken)
    {
        var isFileAction = action.Kind is ActionKind.BrowseFolder or ActionKind.OpenFile;
        if (isFileAction)
        {
            var closed = await CloseCurrentAsync(cancellationToken);
            if (!closed.Succeeded) return closed;
            var outcome = await explorer.ExecuteAsync(action, cancellationToken);
            if (outcome.Succeeded) _owner = WindowOwner.Explorer;
            return outcome;
        }

        if (_owner == WindowOwner.Explorer)
        {
            var closed = await CloseCurrentAsync(cancellationToken);
            if (!closed.Succeeded) return closed;
        }

        var browserOutcome = await browser.ExecuteAsync(action, cancellationToken);
        _owner = WindowOwner.Browser;
        if (!browserOutcome.Succeeded) return browserOutcome;
        var closeAfter = action.Parameters?.TryGetValue("closeAfter", out var value) == true &&
            bool.TryParse(value, out var shouldClose) && shouldClose;
        if (!closeAfter) return browserOutcome;

        var browserClosed = await CloseCurrentAsync(cancellationToken);
        return browserClosed.Succeeded ? browserOutcome : browserClosed;
    }

    private async Task<ActionOutcome> CloseCurrentAsync(CancellationToken cancellationToken)
    {
        if (_owner == WindowOwner.None) return new(true, "window.none");
        var active = _owner == WindowOwner.Browser ? browser : explorer;
        var outcome = await active.ExecuteAsync(new(ActionKind.CloseWindow, "owned"), cancellationToken);
        if (outcome.Succeeded)
        {
            _owner = WindowOwner.None;
        }
        return outcome;
    }

    private enum WindowOwner { None, Browser, Explorer }
}
