using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using CryoChaos.Models;
using CryoChaos.Services;
using CryoChaos.Services.Rendering;

namespace CryoChaos.Views;

/// <summary>
/// Native child HWND used exclusively by the low-latency DXGI swap chain.
/// WPF never copies or transforms captured pixels.
/// </summary>
public sealed class D3D11PreviewHost : HwndHost
{
    private const int WmNcHitTest = 0x0084;
    private static readonly IntPtr HtTransparent = new(-1);

    private HwndSource? _childSource;
    private D3D11ScreenEffectRenderer? _renderer;

    public double CurrentFps => _renderer?.CurrentFps ?? 0;
    public long PresentedFrames => _renderer?.PresentedFrames ?? 0;
    public (int Width, int Height) CaptureSize =>
        _renderer?.CaptureSize ?? (0, 0);
    public (int Width, int Height) OutputSize =>
        _renderer?.OutputSize ?? (0, 0);

    public void StartCapture(IntPtr sourceWindow, ScreenTransformMode mode)
    {
        if (_renderer is null)
        {
            throw new InvalidOperationException(
                "The Direct3D preview surface has not been created yet.");
        }

        // BuildWindowCore can run before WPF has completed the first layout
        // pass.  In that case the native host and its swap chain are born at
        // 1x1 and OnWindowPositionChanged may already have fired before the
        // renderer was assigned.  Read the final HWND client size here so the
        // first captured frame is presented to a visible back buffer.
        RefreshSurfaceSize();
        _renderer.StartCapture(sourceWindow, mode);

        // WPF can perform one more HwndHost arrange after Loaded.  Re-read the
        // native pixel size on the dispatcher instead of guessing from DIPs.
        Dispatcher.BeginInvoke(RefreshSurfaceSize);
    }

    public void StopCapture() => _renderer?.StopCapture();

    public void StartMonitorCapture(
        IntPtr monitor,
        ScreenTransformMode mode)
    {
        if (_renderer is null)
        {
            throw new InvalidOperationException(
                "The Direct3D preview surface has not been created yet.");
        }

        RefreshSurfaceSize();
        _renderer.StartMonitorCapture(monitor, mode);
        Dispatcher.BeginInvoke(RefreshSurfaceSize);
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        HwndSourceParameters parameters = new("CryoChaosDX11Preview")
        {
            ParentWindow = hwndParent.Handle,
            WindowStyle = NativeMethods.WS_CHILD |
                          NativeMethods.WS_VISIBLE |
                          NativeMethods.WS_CLIPCHILDREN |
                          NativeMethods.WS_CLIPSIBLINGS,
            Width = Math.Max(1, (int)ActualWidth),
            Height = Math.Max(1, (int)ActualHeight)
        };

        _childSource = new HwndSource(parameters);
        _childSource.AddHook(ChildWindowProcedure);

        IntPtr childWindow = _childSource.Handle;
        int style = NativeMethods.GetWindowLong(
            childWindow,
            NativeMethods.GWL_EXSTYLE);

        // The top-level transform window is already click-through. Applying
        // WS_EX_TRANSPARENT to a DXGI presentation HWND can postpone its paint
        // behind siblings and make the swap-chain output appear missing.
        NativeMethods.SetWindowLong(
            childWindow,
            NativeMethods.GWL_EXSTYLE,
            style |
            NativeMethods.WS_EX_TOOLWINDOW |
            NativeMethods.WS_EX_NOACTIVATE);

        _renderer = new D3D11ScreenEffectRenderer(childWindow);
        RefreshSurfaceSize();
        return new HandleRef(this, childWindow);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        D3D11ScreenEffectRenderer? renderer = _renderer;
        _renderer = null;

        try
        {
            renderer?.Dispose();
        }
        catch (Exception exception)
        {
            // HwndHost invokes this asynchronously during native teardown.
            // Cleanup must never escape into the WPF dispatcher.
            CrashLogService.WriteException(
                "PREVIEW HOST RENDERER CLEANUP",
                exception);
        }

        if (_childSource is not null)
        {
            HwndSource childSource = _childSource;
            _childSource = null;

            try
            {
                childSource.RemoveHook(ChildWindowProcedure);
                childSource.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception exception)
            {
                CrashLogService.WriteException(
                    "PREVIEW HOST HWND CLEANUP",
                    exception);
            }
        }
    }

    protected override void OnWindowPositionChanged(Rect rcBoundingBox)
    {
        base.OnWindowPositionChanged(rcBoundingBox);
        RefreshSurfaceSize();
    }

    public void RefreshSurfaceSize()
    {
        if (_childSource is null || _renderer is null)
        {
            return;
        }

        IntPtr childWindow = _childSource.Handle;
        if (!NativeMethods.GetClientRect(
                childWindow,
                out NativeRect clientRectangle))
        {
            return;
        }

        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        int arrangedWidth = (int)Math.Round(ActualWidth * dpi.DpiScaleX);
        int arrangedHeight = (int)Math.Round(ActualHeight * dpi.DpiScaleY);

        int width = Math.Max(
            1,
            arrangedWidth > 1 ? arrangedWidth : clientRectangle.Width);
        int height = Math.Max(
            1,
            arrangedHeight > 1 ? arrangedHeight : clientRectangle.Height);

        NativeMethods.SetWindowPos(
            childWindow,
            IntPtr.Zero,
            0,
            0,
            width,
            height,
            NativeMethods.SWP_NOZORDER |
            NativeMethods.SWP_NOACTIVATE |
            NativeMethods.SWP_SHOWWINDOW);

        _renderer.Resize(width, height);
    }

    private static IntPtr ChildWindowProcedure(
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

        return IntPtr.Zero;
    }
}
