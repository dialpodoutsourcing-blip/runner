namespace GhostUserRunner.Infrastructure.Desktop;

public enum WindowTarget { Browser, Explorer, Viewer }

public interface IWindowApi
{
    nint Find(WindowTarget target);
    nint FindBrowser(string marker);
    bool IsMinimized(nint handle);
    bool Restore(nint handle);
    bool BringToForeground(nint handle);
    bool IsForeground(nint handle);
}

public interface IWindowFocusCoordinator
{
    bool TryActivate(WindowTarget target);
    bool TryActivateBrowser(string marker) => false;
}

public sealed class WindowFocusCoordinator(IWindowApi windows) : IWindowFocusCoordinator
{
    public bool TryActivate(WindowTarget target)
    {
        var handle = windows.Find(target);
        if (handle == nint.Zero) return false;
        if (windows.IsMinimized(handle) && !windows.Restore(handle)) return false;
        return windows.BringToForeground(handle) && windows.IsForeground(handle);
    }

    public bool TryActivateBrowser(string marker)
    {
        var handle = windows.FindBrowser(marker);
        if (handle == nint.Zero) return false;
        if (windows.IsMinimized(handle) && !windows.Restore(handle)) return false;
        return windows.BringToForeground(handle) && windows.IsForeground(handle);
    }
}
