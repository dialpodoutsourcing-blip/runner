using System.Drawing;

namespace GhostUserRunner.Infrastructure.Safety;

public sealed class StopMonitorState
{
    private TimeSpan? _chordStarted;
    private readonly Queue<TimeSpan> _cornerEntries = new();
    private bool _wasInCorner;

    public bool SampleChord(bool held, TimeSpan now)
    {
        if (!held) { _chordStarted = null; return false; }
        _chordStarted ??= now;
        return now - _chordStarted >= TimeSpan.FromSeconds(2);
    }

    public bool SamplePointer(Point point, TimeSpan now)
    {
        var inCorner = point.X is >= 0 and < 5 && point.Y is >= 0 and < 5;
        if (inCorner && !_wasInCorner) _cornerEntries.Enqueue(now);
        _wasInCorner = inCorner;
        while (_cornerEntries.Count > 0 && now - _cornerEntries.Peek() > TimeSpan.FromSeconds(5)) _cornerEntries.Dequeue();
        return _cornerEntries.Count >= 3;
    }
}
