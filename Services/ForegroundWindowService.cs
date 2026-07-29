using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CryoChaos.Services;

public static class ForegroundWindowService
{
    private const int CursorShowing = 0x00000001;

    public static bool IsWindowForeground(IntPtr window) =>
        window != IntPtr.Zero && GetForegroundWindow() == window;

    public static bool TryActivateDestinyWindow()
    {
        IntPtr window = DestinyWindowService.FindDestinyWindow();
        if (!DestinyWindowService.IsUsableWindow(window))
        {
            return false;
        }

        return SetForegroundWindow(window);
    }

    public static bool IsDestinyForeground()
    {
        IntPtr window = GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return false;
        }

        _ = GetWindowThreadProcessId(window, out uint processId);
        try
        {
            using Process process = Process.GetProcessById((int)processId);
            return process.ProcessName.Equals("destiny2", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsSystemCursorVisible()
    {
        CursorInfo info = new()
        {
            Size = (uint)Marshal.SizeOf<CursorInfo>()
        };

        return GetCursorInfo(ref info) &&
            (info.Flags & CursorShowing) != 0;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorInfo(ref CursorInfo cursorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo
    {
        public uint Size;
        public int Flags;
        public IntPtr Cursor;
        public NativePoint ScreenPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}
