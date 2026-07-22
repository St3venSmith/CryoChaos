using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Threading;
using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Views;

public partial class CaptureDiagnosticWindow : Window
{
    private const int GwlExStyle = -20;
    private const long WsExTransparent = 0x00000020L;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const int WmNcHitTest = 0x0084;
    private const int WmMouseActivate = 0x0021;
    private const int WmSetCursor = 0x0020;
    private static readonly IntPtr HtTransparent = new(-1);
    private static readonly IntPtr MaNoActivate = new(3);
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;

    private readonly IntPtr _destinyWindow;
    private readonly bool _captureMonitor;
    private readonly bool _isEffectOverlay;
    private readonly ScreenTransformMode _effectMode;
    private readonly DispatcherTimer _statusTimer;
    private DateTime _startedAt;
    private HwndSource? _hwndSource;
    private bool _promotedToOverlay;
    private int _cursorHideCalls;

    public CaptureDiagnosticWindow(
        IntPtr destinyWindow,
        bool captureMonitor)
    {
        InitializeComponent();
        _destinyWindow = destinyWindow;
        _captureMonitor = captureMonitor;
        _effectMode = ScreenTransformMode.None;

        Title = captureMonitor
            ? "CryoChaos Diagnostic - Destiny Monitor"
            : "CryoChaos Diagnostic - Destiny Window";

        HelpText.Text = captureMonitor
            ? "This copies the entire monitor containing Destiny. Seeing the desktop or a hall-of-mirrors effect proves Windows Graphics Capture and Direct3D rendering work."
            : "This copies only Destiny's window. A moving game image proves the game-window capture path works.";

        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _statusTimer.Tick += StatusTimer_Tick;

        Loaded += Window_Loaded;
        Closed += Window_Closed;
    }

    /// <summary>
    /// Uses the proven diagnostic preview path for a production effect. The
    /// window begins as an ordinary preview and is promoted to a borderless,
    /// click-through overlay only after Direct3D presents its first frame.
    /// </summary>
    public CaptureDiagnosticWindow(
        IntPtr destinyWindow,
        ScreenTransformMode effectMode)
    {
        InitializeComponent();
        _destinyWindow = destinyWindow;
        _captureMonitor = false;
        _isEffectOverlay = true;
        _effectMode = effectMode;

        Title = "CryoChaos Live Effect Preview";
        Width = 640;
        Height = 360;
        MinWidth = 1;
        MinHeight = 1;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowActivated = false;
        ShowInTaskbar = false;
        Focusable = false;
        Cursor = Cursors.None;
        ForceCursor = true;

        HelpText.Text = "Waiting for the first captured frame...";

        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _statusTimer.Tick += StatusTimer_Tick;

        SourceInitialized += Window_SourceInitialized;
        Loaded += Window_Loaded;
        Closed += Window_Closed;
    }

    public IntPtr NativeHandle => new WindowInteropHelper(this).Handle;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            LiveRenderer.RefreshSurfaceSize();

            if (_captureMonitor)
            {
                IntPtr monitor = DestinyWindowService.GetMonitorHandle(
                    _destinyWindow);
                LiveRenderer.StartMonitorCapture(
                    monitor,
                    ScreenTransformMode.None);
            }
            else
            {
                LiveRenderer.StartCapture(
                    _destinyWindow,
                    _effectMode);
            }

            _startedAt = DateTime.UtcNow;
            _statusTimer.Start();
            StatusTimer_Tick(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"START FAILED: {ex.GetType().Name}: {ex.Message}";
            StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
        }
    }

    private void StatusTimer_Tick(object? sender, EventArgs e)
    {
        long frames = LiveRenderer.PresentedFrames;
        (int captureWidth, int captureHeight) = LiveRenderer.CaptureSize;
        (int outputWidth, int outputHeight) = LiveRenderer.OutputSize;

        StatusText.Text =
            $"Frames: {frames:N0}   FPS: {LiveRenderer.CurrentFps:F1}   " +
            $"Capture: {captureWidth}x{captureHeight}   " +
            $"Output: {outputWidth}x{outputHeight}";

        bool stalled = frames == 0 &&
            DateTime.UtcNow - _startedAt > TimeSpan.FromSeconds(3);

        StatusText.Foreground = stalled
            ? System.Windows.Media.Brushes.OrangeRed
            : System.Windows.Media.Brushes.White;

        if (stalled)
        {
            HelpText.Text =
                "NO FRAMES RECEIVED. Try the other capture test and check the Event log. " +
                "Also use Borderless Windowed mode and keep Destiny unminimized.";
        }

        if (_isEffectOverlay && frames > 0 && !_promotedToOverlay)
        {
            PromoteToFullscreenOverlay();
        }
    }

    private void PromoteToFullscreenOverlay()
    {
        _promotedToOverlay = true;

        DiagnosticsPanel.Visibility = Visibility.Collapsed;
        DiagnosticsRow.Height = new GridLength(0);
        PreviewBorder.Margin = new Thickness(0);
        PreviewBorder.BorderThickness = new Thickness(0);

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Topmost = true;

        NativeRect bounds =
            DestinyWindowService.GetMonitorBounds(_destinyWindow);
        uint dpi = GetDpiForWindow(_destinyWindow);
        double scale = (dpi == 0 ? 96.0 : dpi) / 96.0;

        Left = bounds.Left / scale;
        Top = bounds.Top / scale;
        Width = bounds.Width / scale;
        Height = bounds.Height / scale;

        SetWindowPos(
            NativeHandle,
            HwndTopmost,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            SwpNoActivate | SwpShowWindow);

        LiveRenderer.RefreshSurfaceSize();

        // WGC already excludes the source cursor. Hide the real Windows
        // cursor as well while this fullscreen copy is active so a second
        // pointer is not drawn over the transformed image. Balance every
        // ShowCursor call when the effect window closes.
        int cursorCount;
        do
        {
            cursorCount = ShowCursor(false);
            _cursorHideCalls++;
        }
        while (cursorCount >= 0 && _cursorHideCalls < 32);

        SetCursor(IntPtr.Zero);

        // Install cross-process click-through only after the first frame has
        // proved that the swap chain is presenting. WM_NCHITTEST remains as a
        // second layer, while WS_EX_TRANSPARENT lets input reach Destiny even
        // though it runs on another UI thread and in another process.
        IntPtr currentStyle = GetWindowLongPtr(NativeHandle, GwlExStyle);
        long clickThroughStyle = currentStyle.ToInt64() |
                                 WsExTransparent |
                                 WsExToolWindow |
                                 WsExNoActivate;
        SetWindowLongPtr(
            NativeHandle,
            GwlExStyle,
            new IntPtr(clickThroughStyle));
        SetWindowPos(
            NativeHandle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNoSize |
            SwpNoMove |
            SwpNoZOrder |
            SwpNoActivate |
            SwpFrameChanged);
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        _hwndSource = HwndSource.FromHwnd(NativeHandle);
        _hwndSource?.AddHook(WindowProcedure);
    }

    private IntPtr WindowProcedure(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmNcHitTest)
        {
            handled = true;
            return HtTransparent;
        }

        if (message == WmMouseActivate)
        {
            handled = true;
            return MaNoActivate;
        }

        if (_isEffectOverlay && message == WmSetCursor)
        {
            SetCursor(IntPtr.Zero);
            handled = true;
            return new IntPtr(1);
        }

        return IntPtr.Zero;
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        _statusTimer.Stop();
        LiveRenderer.StopCapture();

        while (_cursorHideCalls > 0)
        {
            ShowCursor(true);
            _cursorHideCalls--;
        }

        if (_hwndSource is not null)
        {
            _hwndSource.RemoveHook(WindowProcedure);
            _hwndSource = null;
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ShowCursor(
        [MarshalAs(UnmanagedType.Bool)] bool show);

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr cursor);

    private static IntPtr GetWindowLongPtr(IntPtr hwnd, int index) =>
        IntPtr.Size == 8
            ? GetWindowLongPtr64(hwnd, index)
            : new IntPtr(GetWindowLong32(hwnd, index));

    private static IntPtr SetWindowLongPtr(
        IntPtr hwnd,
        int index,
        IntPtr newValue) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(hwnd, index, newValue)
            : new IntPtr(SetWindowLong32(hwnd, index, newValue.ToInt32()));

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(
        IntPtr hwnd,
        int index,
        int newValue);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr64(
        IntPtr hwnd,
        int index,
        IntPtr newValue);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
