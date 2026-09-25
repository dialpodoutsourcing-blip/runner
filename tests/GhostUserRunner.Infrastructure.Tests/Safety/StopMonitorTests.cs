using System.Drawing;
using GhostUserRunner.Infrastructure.Safety;

namespace GhostUserRunner.Infrastructure.Tests.Safety;

public sealed class StopMonitorTests
{
    [Fact]
    public void ChordMustRemainHeldForTwoSeconds()
    {
        var monitor = new StopMonitorState();
        Assert.False(monitor.SampleChord(true, TimeSpan.Zero));
        Assert.False(monitor.SampleChord(true, TimeSpan.FromSeconds(1.9)));
        Assert.True(monitor.SampleChord(true, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void ThreeDistinctCornerEntriesWithinFiveSecondsStop()
    {
        var monitor = new StopMonitorState();
        Assert.False(monitor.SamplePointer(new Point(0, 0), TimeSpan.Zero));
        Assert.False(monitor.SamplePointer(new Point(50, 50), TimeSpan.FromSeconds(1)));
        Assert.False(monitor.SamplePointer(new Point(0, 0), TimeSpan.FromSeconds(2)));
        Assert.False(monitor.SamplePointer(new Point(50, 50), TimeSpan.FromSeconds(3)));
        Assert.True(monitor.SamplePointer(new Point(0, 0), TimeSpan.FromSeconds(4)));
    }

    [Fact]
    public void RemainingInCornerCountsAsOneEntry()
    {
        var monitor = new StopMonitorState();
        Assert.False(monitor.SamplePointer(new Point(0, 0), TimeSpan.Zero));
        Assert.False(monitor.SamplePointer(new Point(1, 1), TimeSpan.FromSeconds(1)));
        Assert.False(monitor.SamplePointer(new Point(2, 2), TimeSpan.FromSeconds(2)));
    }
}
