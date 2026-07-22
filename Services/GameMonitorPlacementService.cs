using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CryoChaos.Services;

internal static class GameMonitorPlacementService
{
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpFrameChanged = 0x0020;
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const uint WmClose = 0x0010;

    public static void CenterOnGameMonitor(Window window, bool activate)
    {
        IntPtr game = DestinyWindowService.FindDestinyWindow();
        if (!DestinyWindowService.IsUsableWindow(game)) return;

        NativeRect monitor = DestinyWindowService.GetMonitorBounds(game);
        IntPtr hwnd = new WindowInteropHelper(window).Handle;
        var source = HwndSource.FromHwnd(hwnd);
        double scaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1;
        double scaleY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1;
        int width = Math.Max(1, (int)Math.Round(window.Width * scaleX));
        int height = Math.Max(1, (int)Math.Round(window.Height * scaleY));
        int left = monitor.Left + Math.Max(0, (monitor.Width - width) / 2);
        int top = monitor.Top + Math.Max(0, (monitor.Height - height) / 2);
        _ = SetWindowPos(hwnd, HwndTopmost, left, top, width, height,
            SwpShowWindow | (activate ? 0u : SwpNoActivate));
    }

    public static void FillGameMonitor(Window window, bool activate)
    {
        IntPtr game = DestinyWindowService.FindDestinyWindow();
        if (!DestinyWindowService.IsUsableWindow(game)) return;
        NativeRect monitor = DestinyWindowService.GetMonitorBounds(game);
        _ = SetWindowPos(new WindowInteropHelper(window).Handle, HwndTopmost,
            monitor.Left, monitor.Top, monitor.Width, monitor.Height,
            SwpShowWindow | (activate ? 0u : SwpNoActivate));
    }

    public static void PlaceMiniPlayer(IntPtr window)
    {
        IntPtr game = DestinyWindowService.FindDestinyWindow();
        if (window == IntPtr.Zero || !DestinyWindowService.IsUsableWindow(game)) return;
        NativeRect monitor = DestinyWindowService.GetMonitorBounds(game);
        int width = Math.Clamp(monitor.Width / 3, 480, 720);
        int height = width * 9 / 16;
        int margin = Math.Max(20, monitor.Width / 100);
        _ = SetWindowPos(window, HwndTopmost,
            monitor.Right - width - margin,
            monitor.Bottom - height - margin,
            width, height, SwpShowWindow | SwpNoActivate);
    }

    public static void MakeMiniPlayerClickThrough(IntPtr window)
    {
        if (window == IntPtr.Zero) return;
        int style = GetWindowLong(window, GwlExStyle);
        _ = SetWindowLong(window, GwlExStyle,
            style | WsExTransparent | WsExToolWindow | WsExNoActivate);
        _ = SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoZOrder |
            SwpNoActivate | SwpFrameChanged);
    }

    public static void CloseMiniPlayer(IntPtr window)
    {
        if (window != IntPtr.Zero)
        {
            _ = PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(
        IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam);
}
