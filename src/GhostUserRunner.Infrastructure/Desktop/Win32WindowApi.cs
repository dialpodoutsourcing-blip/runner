using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GhostUserRunner.Infrastructure.Desktop;

public sealed class Win32WindowApi : IWindowApi
{
    public nint Find(WindowTarget target)
    {
        var names = target switch
        {
            WindowTarget.Browser => new[] { "chrome", "msedge" },
            WindowTarget.Explorer => new[] { "explorer" },
            WindowTarget.Viewer => new[] { "notepad" },
            _ => []
        };
        nint found = nint.Zero;
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle)) return true;
            GetWindowThreadProcessId(handle, out var processId);
            try
            {
                using var process = Process.GetProcessById((int)processId);
                if (names.Contains(process.ProcessName, StringComparer.OrdinalIgnoreCase))
                {
                    found = handle;
                    return false;
                }
            }
            catch (ArgumentException) { }
            return true;
        }, nint.Zero);
        return found;
    }

    public bool IsMinimized(nint handle) => IsIconic(handle);
    public bool Restore(nint handle) => ShowWindow(handle, 9);
    public bool BringToForeground(nint handle) => SetForegroundWindow(handle);
    public bool IsForeground(nint handle) => GetForegroundWindow() == handle;

    public nint FindBrowser(string marker)
    {
        nint found = nint.Zero;
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle)) return true;
            var title = new System.Text.StringBuilder(512);
            GetWindowText(handle, title, title.Capacity);
            if (!title.ToString().Contains(marker, StringComparison.Ordinal)) return true;
            GetWindowThreadProcessId(handle, out var processId);
            try
            {
                using var process = Process.GetProcessById((int)processId);
                var path = process.MainModule?.FileName ?? string.Empty;
                if (process.ProcessName.Equals("chrome", StringComparison.OrdinalIgnoreCase) && path.Contains(".playwright", StringComparison.OrdinalIgnoreCase))
                { found = handle; return false; }
            }
            catch (Exception exception) when (exception is ArgumentException or System.ComponentModel.Win32Exception or InvalidOperationException) { }
            return true;
        }, nint.Zero);
        return found;
    }

    private delegate bool EnumWindowsProc(nint handle, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint handle);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint handle);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint handle, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint handle);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint handle, System.Text.StringBuilder text, int maximum);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle, out uint processId);
}
