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

        Assert.Equal(1, browser.Count);
        Assert.Equal(1, explorer.Count);
    }

    private sealed class RecordingExecutor(string process) : IActionExecutor
    {
        public int Count { get; private set; }
        public ObservedContext Observe() => new(true, process, null, null);
        public Task<ActionOutcome> ExecuteAsync(ProposedAction action, CancellationToken cancellationToken)
        { Count++; return Task.FromResult(new ActionOutcome(true, "ok")); }
    }
}
