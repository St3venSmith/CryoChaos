using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Views;

public partial class ScreenTransformWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private static readonly IntPtr HwndTopmost = new(-1);

    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private readonly IntPtr _sourceWindow;
    private readonly ScreenTransformMode _mode;
    private readonly IntPtr[] _excludedWindows;
    private readonly NativeRect _monitorBounds;
    private readonly DispatcherTimer _sourceTimer;

    public ScreenTransformWindow(
        IntPtr sourceWindow,
        ScreenTransformMode mode,
        IEnumerable<IntPtr> excludedWindows)
    {
        if (!DestinyWindowService.IsUsableWindow(sourceWindow))
        {
            throw new InvalidOperationException(
                "Destiny 2 is not running in a usable window.");
        }

        InitializeComponent();

        _sourceWindow = sourceWindow;
        _mode = mode;
        _excludedWindows = excludedWindows
            .Where(handle => handle != IntPtr.Zero)
            .Distinct()
            .ToArray();
        _monitorBounds = DestinyWindowService.GetMonitorBounds(sourceWindow);

        uint dpi = GetDpiForWindow(sourceWindow);
        double dpiScale = (dpi == 0 ? 96.0 : dpi) / 96.0;

        Left = _monitorBounds.Left / dpiScale;
        Top = _monitorBounds.Top / dpiScale;
        Width = _monitorBounds.Width / dpiScale;
        Height = _monitorBounds.Height / dpiScale;

        if (!DestinyWindowService.TryGetClientScreenRect(
                _sourceWindow,
                out NativeRect initialSourceRectangle))
        {
            throw new InvalidOperationException(
                "The Destiny 2 client area could not be captured.");
        }

        LiveMagnifier.Configure(
            initialSourceRectangle,
            _mode,
            _excludedWindows);

        _sourceTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _sourceTimer.Tick += SourceTimer_Tick;

        SourceInitialized += ScreenTransformWindow_SourceInitialized;
        Loaded += ScreenTransformWindow_Loaded;
        Closed += ScreenTransformWindow_Closed;
    }

    private void ScreenTransformWindow_SourceInitialized(
        object? sender,
        EventArgs e)
    {
        IntPtr window = new WindowInteropHelper(this).Handle;

        LiveMagnifier.SetExcludedWindows(
            _excludedWindows.Append(window));

        int style = GetWindowLong(window, GwlExStyle);
        SetWindowLong(
            window,
            GwlExStyle,
            style |
            WsExTransparent |
            WsExToolWindow |
            WsExNoActivate);

        SetWindowPos(
            window,
            HwndTopmost,
            _monitorBounds.Left,
            _monitorBounds.Top,
            _monitorBounds.Width,
            _monitorBounds.Height,
            SwpNoActivate | SwpShowWindow);
    }

    private void ScreenTransformWindow_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        _sourceTimer.Start();
    }

    private void SourceTimer_Tick(
        object? sender,
        EventArgs e)
    {
        if (!DestinyWindowService.TryGetClientScreenRect(
                _sourceWindow,
                out NativeRect sourceRectangle))
        {
            LiveMagnifier.Visibility = Visibility.Hidden;
            return;
        }

        LiveMagnifier.Visibility = Visibility.Visible;
        LiveMagnifier.UpdateSourceRectangle(sourceRectangle);
    }

    private void ScreenTransformWindow_Closed(
        object? sender,
        EventArgs e)
    {
        _sourceTimer.Stop();
        _sourceTimer.Tick -= SourceTimer_Tick;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(
        IntPtr window,
        int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(
        IntPtr window,
        int index,
        int newStyle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
