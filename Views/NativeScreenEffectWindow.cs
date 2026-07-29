using System.Runtime.InteropServices;
using CryoChaos.Models;
using CryoChaos.Services;
using CryoChaos.Services.Rendering;

namespace CryoChaos.Views;

/// <summary>
/// Pure Win32/DX11 production overlay. Unlike HwndHost, this has no WPF
/// parent or child input window between Destiny and the desktop hit test.
/// </summary>
internal sealed class NativeScreenEffectWindow : IDisposable
{
    private const int WsExTopmost = 0x00000008;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExLayered = 0x00080000;
    private const int WsExNoActivate = 0x08000000;
    private const int WsPopup = unchecked((int)0x80000000);
    private const uint LwaAlpha = 0x00000002;
    private const int SwShowNoActivate = 4;

    private D3D11ScreenEffectRenderer? _renderer;

    public NativeScreenEffectWindow(
        IntPtr destinyWindow,
        IReadOnlyList<ScreenTransformMode> modes)
    {
        NativeRect bounds =
            DestinyWindowService.GetMonitorBounds(destinyWindow);

        int extendedStyle =
            WsExTopmost |
            WsExTransparent |
            WsExToolWindow |
            WsExLayered |
            WsExNoActivate;

        NativeHandle = CreateWindowEx(
            extendedStyle,
            "STATIC",
            "CryoChaosScreenEffect",
            WsPopup,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            IntPtr.Zero,
            IntPtr.Zero,
            GetModuleHandle(null),
            IntPtr.Zero);

        if (NativeHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                $"Could not create the native effect window. Win32 error={Marshal.GetLastWin32Error()}.");
        }

        try
        {
            SetLayeredWindowAttributes(
                NativeHandle,
                0,
                byte.MaxValue,
                LwaAlpha);

            _renderer = new D3D11ScreenEffectRenderer(NativeHandle);
            _renderer.Resize(bounds.Width, bounds.Height);
            _renderer.StartCapture(destinyWindow, modes);
            ShowWindow(NativeHandle, SwShowNoActivate);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public IntPtr NativeHandle { get; private set; }

    public bool IsVisible =>
        NativeHandle != IntPtr.Zero &&
        IsWindowVisible(NativeHandle);

    public void SetEffectModes(IReadOnlyList<ScreenTransformMode> modes) =>
        _renderer?.SetEffectModes(modes);

    public void SetFilterSettings(ScreenFilterSettings settings) =>
        _renderer?.SetFilterSettings(settings);

    public void Close() => Dispose();

    public void Dispose()
    {
        D3D11ScreenEffectRenderer? renderer = _renderer;
        _renderer = null;

        try
        {
            renderer?.Dispose();
        }
        catch (Exception exception)
        {
            CrashLogService.WriteException(
                "NATIVE SCREEN EFFECT RENDERER CLEANUP",
                exception);
        }

        IntPtr window = NativeHandle;
        NativeHandle = IntPtr.Zero;
        if (window != IntPtr.Zero)
        {
            DestroyWindow(window);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);

    [DllImport(
        "user32.dll",
        EntryPoint = "CreateWindowExW",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern IntPtr CreateWindowEx(
        int extendedStyle,
        string className,
        string windowName,
        int style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetLayeredWindowAttributes(
        IntPtr window,
        uint colorKey,
        byte alpha,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr window);
}
