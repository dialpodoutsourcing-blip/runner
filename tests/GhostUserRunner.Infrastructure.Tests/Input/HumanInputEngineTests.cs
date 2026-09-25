using System.Drawing;
using GhostUserRunner.Core.Randomness;
using GhostUserRunner.Infrastructure.Input;

namespace GhostUserRunner.Infrastructure.Tests.Input;

public sealed class HumanInputEngineTests
{
    [Fact]
    public async Task ClickBalancesMouseDownAndUp()
    {
        var sink = new RecordingInputSink();
        var engine = new HumanInputEngine(sink, new SeededRandomSource(1));
        await engine.ClickAsync(new Point(12, 8), CancellationToken.None);
        Assert.Equal(1, sink.DownCount);
        Assert.Equal(1, sink.UpCount);
    }

    [Fact]
    public async Task CancellationAlwaysReleasesInput()
    {
        var sink = new RecordingInputSink();
        var engine = new HumanInputEngine(sink, new SeededRandomSource(1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.MoveAsync(new Point(0, 0), new Point(100, 100), cancellation.Token));

        Assert.True(sink.Released);
    }

    private sealed class RecordingInputSink : IInputSink
    {
        public bool Released { get; private set; }
        public int DownCount { get; private set; }
        public int UpCount { get; private set; }
        public void MoveTo(Point point) { }
        public void KeyDown(ushort virtualKey) { }
        public void KeyUp(ushort virtualKey) { }
        public void MouseButtonDown() => DownCount++;
        public void MouseButtonUp() => UpCount++;
        public void Scroll(int amount) { }
        public void ReleaseAll() => Released = true;
    }
}
