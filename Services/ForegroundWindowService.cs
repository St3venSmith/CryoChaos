using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CryoChaos.Services;

public static class ForegroundWindowService
{
    public static bool IsWindowForeground(IntPtr window) =>
        window != IntPtr.Zero && GetForegroundWindow() == window;

    public static bool TryActivateDestinyWindow()
    {
        IntPtr window = DestinyWindowService.FindDestinyWindow();
        if (!DestinyWindowService.IsUsableWindow(window))
        {
            return false;
        }

        // Foreground activation completes asynchronously. The caller waits
        // briefly and verifies the result before sending input.
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

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
}
