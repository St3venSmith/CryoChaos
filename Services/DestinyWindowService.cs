using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CryoChaos.Services;

public static class DestinyWindowService
{
    private const uint MonitorDefaultToNearest = 0x00000002;

    public static IntPtr FindDestinyWindow()
    {
        return Process
            .GetProcessesByName("destiny2")
            .Select(process =>
            {
                try
                {
                    return process.MainWindowHandle;
                }
                finally
                {
                    process.Dispose();
                }
            })
            .FirstOrDefault(handle =>
                handle != IntPtr.Zero &&
                IsWindow(handle) &&
                !IsIconic(handle));
    }

    public static bool IsUsableWindow(IntPtr window)
    {
        return window != IntPtr.Zero &&
               IsWindow(window) &&
               !IsIconic(window);
    }

    internal static bool TryGetClientScreenRect(
        IntPtr window,
        out NativeRect rectangle)
    {
        rectangle = default;

        if (!IsUsableWindow(window) ||
            !GetClientRect(window, out NativeRect clientRectangle))
        {
            return false;
        }

        NativePoint topLeft = new()
        {
            X = clientRectangle.Left,
            Y = clientRectangle.Top
        };

        NativePoint bottomRight = new()
        {
            X = clientRectangle.Right,
            Y = clientRectangle.Bottom
        };

        if (!ClientToScreen(window, ref topLeft) ||
            !ClientToScreen(window, ref bottomRight))
        {
            return false;
        }

        rectangle = new NativeRect
        {
            Left = topLeft.X,
            Top = topLeft.Y,
            Right = bottomRight.X,
            Bottom = bottomRight.Y
        };

        return rectangle.Width > 0 && rectangle.Height > 0;
    }

    internal static NativeRect GetMonitorBounds(IntPtr window)
    {
        IntPtr monitor = GetMonitorHandle(window);

        if (monitor == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "The monitor containing Destiny 2 could not be determined.");
        }

        MonitorInfo monitorInfo = new()
        {
            Size = Marshal.SizeOf<MonitorInfo>()
        };

        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            throw new InvalidOperationException(
                "Windows could not return the game monitor bounds.");
        }

        return monitorInfo.Monitor;
    }

    internal static IntPtr GetMonitorHandle(IntPtr window)
    {
        IntPtr monitor = MonitorFromWindow(
            window,
            MonitorDefaultToNearest);

        if (monitor == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "The monitor containing Destiny 2 could not be determined.");
        }

        return monitor;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(
        IntPtr window,
        out NativeRect rectangle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(
        IntPtr window,
        ref NativePoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(
        IntPtr window,
        uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(
        IntPtr monitor,
        ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }
}
