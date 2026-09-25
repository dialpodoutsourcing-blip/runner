using GhostUserRunner.Infrastructure.Desktop;

namespace GhostUserRunner.Infrastructure.Tests.Desktop;

public sealed class WindowFocusCoordinatorTests
{
    [Fact]
    public void RestoresMinimizedBrowserBeforeBringingItForward()
    {
        var windows = new RecordingWindowApi
        {
            Handles = { [WindowTarget.Browser] = new nint(101) },
            Minimized = { new nint(101) }
        };
        var coordinator = new WindowFocusCoordinator(windows);

        var activated = coordinator.TryActivate(WindowTarget.Browser);

        Assert.True(activated);
        Assert.Equal(["restore:101", "foreground:101"], windows.Operations);
    }

    [Fact]
    public void RefusesToGuessWhenExpectedWindowDoesNotExist()
    {
        var windows = new RecordingWindowApi();

        Assert.False(new WindowFocusCoordinator(windows).TryActivate(WindowTarget.Viewer));
        Assert.Empty(windows.Operations);
    }

    [Fact]
    public void LeavesPreviousWindowOpenBehindNewForegroundWindow()
    {
        var windows = new RecordingWindowApi
        {
            Handles = { [WindowTarget.Viewer] = new nint(202), [WindowTarget.Browser] = new nint(303) }
        };
        var coordinator = new WindowFocusCoordinator(windows);
        Assert.True(coordinator.TryActivate(WindowTarget.Viewer));
        windows.Operations.Clear();

        Assert.True(coordinator.TryActivate(WindowTarget.Browser));

        Assert.Equal(["foreground:303"], windows.Operations);
    }

    [Fact]
    public void BrowserSessionMarkerSelectsOnlyTheOwnedWindow()
    {
        var windows = new RecordingWindowApi { MarkedHandle = new nint(404) };
        var coordinator = new WindowFocusCoordinator(windows);

        Assert.True(coordinator.TryActivateBrowser("ghost-session-123"));
        Assert.Equal("ghost-session-123", windows.RequestedMarker);
        Assert.Equal(["foreground:404"], windows.Operations);
    }

    private sealed class RecordingWindowApi : IWindowApi
    {
        public Dictionary<WindowTarget, nint> Handles { get; } = [];
        public HashSet<nint> Minimized { get; } = [];
        public List<string> Operations { get; } = [];
        public nint MarkedHandle { get; set; }
        public string? RequestedMarker { get; private set; }
        public nint Find(WindowTarget target) => Handles.GetValueOrDefault(target);
        public nint FindBrowser(string marker) { RequestedMarker = marker; return MarkedHandle; }
        public bool IsMinimized(nint handle) => Minimized.Contains(handle);
        public bool Restore(nint handle) { Operations.Add($"restore:{handle}"); return true; }
        public bool BringToForeground(nint handle) { Operations.Add($"foreground:{handle}"); return true; }
        public bool IsForeground(nint handle) => true;
    }
}
