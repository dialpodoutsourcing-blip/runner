using GhostUserRunner.Core.Actions;
using GhostUserRunner.Core.Execution;
using GhostUserRunner.Infrastructure.Execution;

namespace GhostUserRunner.Infrastructure.Tests.Execution;

public sealed class CompositeActionExecutorTests
{
    [Fact]
    public async Task RoutesBrowserAndFileActionsToTheirOwnedAdapters()
    {
        var browser = new RecordingExecutor("browser");
        var explorer = new RecordingExecutor("explorer");
        var composite = new CompositeActionExecutor(browser, explorer);

        await composite.ExecuteAsync(new(ActionKind.SearchWeb, "https://example.test"), CancellationToken.None);
        await composite.ExecuteAsync(new(ActionKind.BrowseFolder, @"C:\Safe"), CancellationToken.None);

        Assert.Equal([ActionKind.SearchWeb, ActionKind.CloseWindow], browser.Actions);
        Assert.Equal([ActionKind.BrowseFolder], explorer.Actions);
    }

    [Fact]
    public async Task ReusesBrowserUntilPreplannedCloseAction()
    {
        var browser = new RecordingExecutor("browser");
        var composite = new CompositeActionExecutor(browser, new RecordingExecutor("explorer"));

        for (var index = 0; index < 4; index++)
            await composite.ExecuteAsync(new(ActionKind.SearchWeb, "https://example.test"), CancellationToken.None);
        await composite.ExecuteAsync(new(ActionKind.SearchWeb, "https://example.test",
            new Dictionary<string, string> { ["closeAfter"] = "true" }), CancellationToken.None);

        Assert.Equal(
            [ActionKind.SearchWeb, ActionKind.SearchWeb, ActionKind.SearchWeb, ActionKind.SearchWeb, ActionKind.SearchWeb, ActionKind.CloseWindow],
            browser.Actions);
    }

    private sealed class RecordingExecutor(string process) : IActionExecutor
    {
        public List<ActionKind> Actions { get; } = [];
        public ObservedContext Observe() => new(true, process, null, null);
        public Task<ActionOutcome> ExecuteAsync(ProposedAction action, CancellationToken cancellationToken)
        { Actions.Add(action.Kind); return Task.FromResult(new ActionOutcome(true, "ok")); }
    }
}
