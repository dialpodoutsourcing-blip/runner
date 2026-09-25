using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace GhostUserRunner.Infrastructure.Input;

public sealed class Win32InputSink : IInputSink
{
    private readonly HashSet<ushort> _heldKeys = [];

    public void MoveTo(Point point)
    {
        if (!SetCursorPos(point.X, point.Y)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public void KeyDown(ushort virtualKey) { SendKeyboard(virtualKey, false); _heldKeys.Add(virtualKey); }
    public void KeyUp(ushort virtualKey) { SendKeyboard(virtualKey, true); _heldKeys.Remove(virtualKey); }
    public void MouseButtonDown() => SendMouse(MouseEventLeftDown);
    public void MouseButtonUp() => SendMouse(MouseEventLeftUp);
    public void Scroll(int amount) => SendMouse(MouseEventWheel, amount);

    public void ReleaseAll()
    {
        foreach (var key in _heldKeys.ToArray()) KeyUp(key);
        MouseButtonUp();
    }

    private static void SendKeyboard(ushort key, bool keyUp)
    {
        var input = new Input { Type = 1, Union = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = key, Flags = keyUp ? 2u : 0u } } };
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private static void SendMouse(uint flags, int data = 0)
    {
        var input = new Input { Type = 0, Union = new InputUnion { Mouse = new MouseInput { MouseData = data, Flags = flags } } };
        if (SendInput(1, [input], Marshal.SizeOf<Input>()) != 1) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventWheel = 0x0800;
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type; public InputUnion Union; }
    [StructLayout(LayoutKind.Explicit)] private struct InputUnion { [FieldOffset(0)] public MouseInput Mouse; [FieldOffset(0)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int Dx; public int Dy; public int MouseData; public uint Flags; public uint Time; public nint ExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort VirtualKey; public ushort Scan; public uint Flags; public uint Time; public nint ExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
}
