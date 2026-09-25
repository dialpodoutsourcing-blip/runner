using GhostUserRunner.Core.Actions;
using GhostUserRunner.Infrastructure.Simulation;

namespace GhostUserRunner.Infrastructure.Tests.Simulation;

public sealed class SimulationExecutorTests
{
    [Fact]
    public async Task RecordsWithoutControllingDesktop()
    {
        var executor = new SimulationExecutor(new ObservedContext(true, "browser", null, null));

        var outcome = await executor.ExecuteAsync(new(ActionKind.Idle, "idle"), CancellationToken.None);

        Assert.True(outcome.Succeeded);
        Assert.Equal(1, executor.ExecutedCount);
    }
}
