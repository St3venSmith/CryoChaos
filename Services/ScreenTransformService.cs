using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using CryoChaos.Models;
using CryoChaos.Views;

namespace CryoChaos.Services;

public interface IScreenTransformService : IDisposable
{
    bool IsActive { get; }

    Task ShowAsync(
        ScreenTransformMode mode,
        TimeSpan duration,
        CancellationToken cancellationToken);

    Task StopAsync();
}

public sealed class ScreenTransformService : IScreenTransformService
{
    private static readonly IntPtr HwndTopmost = new(-1);

    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private readonly OverlayWindow _overlay;
    private readonly SemaphoreSlim _effectGate = new(1, 1);
    private readonly object _stateLock = new();
    private readonly DispatcherTimer _zOrderTimer;

    private CaptureDiagnosticWindow? _transformWindow;
    private bool _disposed;

    public ScreenTransformService(OverlayWindow overlay)
    {
        _overlay = overlay;

        // Reassert the two-window order while a live transform is active:
        // Destiny -> transformed live view -> CryoChaos HUD/effect overlay.
        // This prevents another topmost update from placing the copied view
        // over the progress bar or current-effect card.
        _zOrderTimer = new DispatcherTimer(DispatcherPriority.Send)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };

        _zOrderTimer.Tick += (_, _) =>
            EnsureOverlayAboveTransform();
    }

    public bool IsActive
    {
        get
        {
            lock (_stateLock)
            {
                return _transformWindow is not null;
            }
        }
    }

    public async Task ShowAsync(
        ScreenTransformMode mode,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(
                nameof(ScreenTransformService));
        }

        await _effectGate.WaitAsync(cancellationToken);

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            IntPtr destinyWindow =
                DestinyWindowService.FindDestinyWindow();

            if (!DestinyWindowService.IsUsableWindow(destinyWindow))
            {
                throw new InvalidOperationException(
                    "Destiny 2 must be running and not minimized for a live screen-transform effect.");
            }

            await _overlay.Dispatcher.InvokeAsync(() =>
            {
                CloseTransformWindowOnUiThread();

                CaptureDiagnosticWindow window = new(
                    destinyWindow,
                    mode);

                lock (_stateLock)
                {
                    _transformWindow = window;
                }

                window.Show();
                EnsureOverlayAboveTransform();
                _zOrderTimer.Start();
            });

            try
            {
                await Task.Delay(duration, cancellationToken);
            }
            finally
            {
                await StopAsync();
            }
        }
        finally
        {
            _effectGate.Release();
        }
    }

    public async Task StopAsync()
    {
        if (_overlay.Dispatcher.HasShutdownStarted ||
            _overlay.Dispatcher.HasShutdownFinished)
        {
            return;
        }

        await _overlay.Dispatcher.InvokeAsync(
            CloseTransformWindowOnUiThread);
    }

    private void CloseTransformWindowOnUiThread()
    {
        _zOrderTimer.Stop();

        CaptureDiagnosticWindow? window;

        lock (_stateLock)
        {
            window = _transformWindow;
            _transformWindow = null;
        }

        if (window is null)
        {
            return;
        }

        window.Close();
    }

    private void EnsureOverlayAboveTransform()
    {
        if (_overlay.Dispatcher.HasShutdownStarted ||
            _overlay.Dispatcher.HasShutdownFinished)
        {
            return;
        }

        CaptureDiagnosticWindow? transformWindow;

        lock (_stateLock)
        {
            transformWindow = _transformWindow;
        }

        if (transformWindow is null ||
            !transformWindow.IsVisible)
        {
            return;
        }

        IntPtr transformHandle = transformWindow.NativeHandle;

        IntPtr overlayHandle =
            new WindowInteropHelper(_overlay).Handle;

        if (transformHandle == IntPtr.Zero ||
            overlayHandle == IntPtr.Zero)
        {
            return;
        }

        const uint flags =
            SwpNoMove |
            SwpNoSize |
            SwpNoActivate |
            SwpShowWindow;

        // Put the transformed live view in the topmost group first.
        SetWindowPos(
            transformHandle,
            HwndTopmost,
            0,
            0,
            0,
            0,
            flags);

        // Then put the normal CryoChaos overlay after it. The most recent
        // HWND_TOPMOST call sits above the previous topmost window.
        SetWindowPos(
            overlayHandle,
            HwndTopmost,
            0,
            0,
            0,
            0,
            flags);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _zOrderTimer.Stop();

        if (!_overlay.Dispatcher.HasShutdownStarted &&
            !_overlay.Dispatcher.HasShutdownFinished)
        {
            if (_overlay.Dispatcher.CheckAccess())
            {
                CloseTransformWindowOnUiThread();
            }
            else
            {
                _overlay.Dispatcher.Invoke(
                    CloseTransformWindowOnUiThread);
            }
        }

        _effectGate.Dispose();
    }

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
