using System.ComponentModel;
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

    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;

    private const int WmNcHitTest = 0x0084;
    private const int WmMouseActivate = 0x0021;

    private static readonly IntPtr HtTransparent = new(-1);
    private static readonly IntPtr MaNoActivate = new(3);
    private static readonly IntPtr HwndTopmost = new(-1);

    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private readonly IntPtr _sourceWindow;
    private readonly ScreenTransformMode _mode;
    private readonly IntPtr[] _excludedWindows;
    private readonly NativeRect _monitorBounds;

    private readonly DispatcherTimer _sourceTimer;

    private NativeRect _currentSourceRectangle;

    private HwndSource? _hwndSource;
    private bool _isClosed;

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

        // Do not allow this overlay to activate or receive mouse input.
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;

        _sourceWindow = sourceWindow;
        _mode = mode;

        _excludedWindows = excludedWindows
            .Where(handle => handle != IntPtr.Zero)
            .Distinct()
            .ToArray();

        _monitorBounds =
            DestinyWindowService.GetMonitorBounds(sourceWindow);

        uint dpi = GetDpiForWindow(sourceWindow);

        double dpiScale =
            (dpi == 0 ? 96.0 : dpi) / 96.0;

        Left = _monitorBounds.Left / dpiScale;
        Top = _monitorBounds.Top / dpiScale;
        Width = _monitorBounds.Width / dpiScale;
        Height = _monitorBounds.Height / dpiScale;

        if (!DestinyWindowService.TryGetClientScreenRect(
                _sourceWindow,
                out _currentSourceRectangle))
        {
            throw new InvalidOperationException(
                "The Destiny 2 client area could not be captured.");
        }

        LiveMagnifier.Configure(
            _currentSourceRectangle,
            _mode,
            _excludedWindows);

        // Fixed refresh loop. No DisplayRefreshRateService required.
        _sourceTimer = new DispatcherTimer(
            DispatcherPriority.Render)
        {
            // Approximately 60 refresh attempts per second.
            Interval = TimeSpan.FromMicroseconds(30),
        };

        _sourceTimer.Tick += SourceTimer_Tick;

        SourceInitialized +=
            ScreenTransformWindow_SourceInitialized;

        Loaded +=
            ScreenTransformWindow_Loaded;

        Closed +=
            ScreenTransformWindow_Closed;
    }

    public IntPtr NativeHandle =>
        new WindowInteropHelper(this).Handle;

    private void ScreenTransformWindow_SourceInitialized(
        object? sender,
        EventArgs e)
    {
        IntPtr window = NativeHandle;

        // Exclude the transform window itself from the magnified view.
        LiveMagnifier.SetExcludedWindows(
            _excludedWindows.Append(window));

        MakeWindowClickThrough(window);

        _hwndSource =
            HwndSource.FromHwnd(window);

        _hwndSource?.AddHook(
            WindowProcedure);

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
        // Keep the normal Windows mouse cursor visible.
        MagnificationNative.MagShowSystemCursor(false);

        // Refresh immediately so there is no startup delay.
        RefreshLiveView();

        _sourceTimer.Start();
    }

    private void SourceTimer_Tick(
        object? sender,
        EventArgs e)
    {
        if (_isClosed)
        {
            return;
        }

        RefreshLiveView();
    }

    private void RefreshLiveView()
    {
        if (!DestinyWindowService.TryGetClientScreenRect(
                _sourceWindow,
                out NativeRect sourceRectangle))
        {
            LiveMagnifier.Visibility =
                Visibility.Hidden;

            return;
        }

        _currentSourceRectangle =
            sourceRectangle;

        if (LiveMagnifier.Visibility !=
            Visibility.Visible)
        {
            LiveMagnifier.Visibility =
                Visibility.Visible;
        }

        try
        {
            LiveMagnifier.RefreshFrame(
                _currentSourceRectangle);
        }
        catch (Win32Exception)
        {
            LiveMagnifier.Visibility =
                Visibility.Hidden;
        }
    }

    private IntPtr WindowProcedure(
        IntPtr window,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        switch (message)
        {
            case WmNcHitTest:
                /*
                 * Tell Windows that the mouse belongs to the
                 * window underneath this overlay.
                 */
                handled = true;
                return HtTransparent;

            case WmMouseActivate:
                /*
                 * Prevent this overlay from activating when
                 * the user clicks through it.
                 */
                handled = true;
                return MaNoActivate;

            default:
                return IntPtr.Zero;
        }
    }

    private static void MakeWindowClickThrough(
        IntPtr window)
    {
        IntPtr currentStyle =
            GetWindowLongPtr(
                window,
                GwlExStyle);

        long updatedStyle =
            currentStyle.ToInt64() |
            WsExTransparent |
            WsExToolWindow |
            WsExNoActivate;

        SetWindowLongPtr(
            window,
            GwlExStyle,
            new IntPtr(updatedStyle));
    }

    private void ScreenTransformWindow_Closed(
        object? sender,
        EventArgs e)
    {
        _isClosed = true;

        _sourceTimer.Stop();
        _sourceTimer.Tick -=
            SourceTimer_Tick;

        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(
                WindowProcedure);

            _hwndSource = null;
        }
    }

    private static IntPtr GetWindowLongPtr(
        IntPtr window,
        int index)
    {
        return IntPtr.Size == 8
            ? GetWindowLongPtr64(
                window,
                index)
            : new IntPtr(
                GetWindowLong32(
                    window,
                    index));
    }

    private static IntPtr SetWindowLongPtr(
        IntPtr window,
        int index,
        IntPtr newValue)
    {
        return IntPtr.Size == 8
            ? SetWindowLongPtr64(
                window,
                index,
                newValue)
            : new IntPtr(
                SetWindowLong32(
                    window,
                    index,
                    newValue.ToInt32()));
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(
        IntPtr window);

    [DllImport(
        "user32.dll",
        EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(
        IntPtr window,
        int index);

    [DllImport(
        "user32.dll",
        EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(
        IntPtr window,
        int index);

    [DllImport(
        "user32.dll",
        EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(
        IntPtr window,
        int index,
        int newValue);

    [DllImport(
        "user32.dll",
        EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(
        IntPtr window,
        int index,
        IntPtr newValue);

    [DllImport(
        "user32.dll",
        SetLastError = true)]
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