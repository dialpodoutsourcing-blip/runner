using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;

namespace GhostUserRunner.Infrastructure.Safety;

public sealed class StopMonitor
{
    private readonly StopMonitorState _state = new();
    public async Task MonitorAsync(Action requestStop, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        while (!cancellationToken.IsCancellationRequested)
        {
            var chord = IsDown(0x11) && IsDown(0x12) && IsDown(0x51);
            GetCursorPos(out var point);
            if (_state.SampleChord(chord, clock.Elapsed) || _state.SamplePointer(point, clock.Elapsed))
            {
                requestStop();
                return;
            }
            await Task.Delay(50, cancellationToken);
        }
    }

    private static bool IsDown(int key) => (GetAsyncKeyState(key) & 0x8000) != 0;
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int virtualKey);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
}
