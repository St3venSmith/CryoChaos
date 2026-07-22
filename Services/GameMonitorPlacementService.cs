using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CryoChaos.Services;

internal static class GameMonitorPlacementService
{
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
