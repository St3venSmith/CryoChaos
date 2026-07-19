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

    private ScreenTransformWindow? _transformWindow;
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

                IntPtr overlayHandle =
                    new WindowInteropHelper(_overlay).Handle;

                IntPtr[] excludedWindows =
                    GetCurrentProcessTopLevelWindows()
                        .Append(overlayHandle)
                        .Where(handle => handle != IntPtr.Zero)
                        .Distinct()
                        .ToArray();

                ScreenTransformWindow window = new(
                    destinyWindow,
                    mode,
                    excludedWindows);

                lock (_stateLock)
                {
                    _transformWindow = window;
                }

                window.ContentRendered +=
                    TransformWindow_ContentRendered;

                window.Closed +=
                    TransformWindow_Closed;

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

    private void TransformWindow_ContentRendered(
        object? sender,
        EventArgs e)
    {
        EnsureOverlayAboveTransform();
    }

    private void TransformWindow_Closed(
        object? sender,
        EventArgs e)
    {
        if (sender is ScreenTransformWindow window)
        {
            window.ContentRendered -=
                TransformWindow_ContentRendered;

            window.Closed -=
                TransformWindow_Closed;
        }

        _zOrderTimer.Stop();
    }

    private void CloseTransformWindowOnUiThread()
    {
        _zOrderTimer.Stop();

        ScreenTransformWindow? window;

        lock (_stateLock)
        {
            window = _transformWindow;
            _transformWindow = null;
        }

        if (window is null)
        {
            return;
        }

        window.ContentRendered -=
            TransformWindow_ContentRendered;

        window.Closed -=
            TransformWindow_Closed;

        window.Close();
    }

    private void EnsureOverlayAboveTransform()
    {
        if (_overlay.Dispatcher.HasShutdownStarted ||
            _overlay.Dispatcher.HasShutdownFinished)
        {
            return;
        }

        ScreenTransformWindow? transformWindow;

        lock (_stateLock)
        {
            transformWindow = _transformWindow;
        }

        if (transformWindow is null ||
            !transformWindow.IsVisible)
        {
            return;
        }

        IntPtr transformHandle =
            transformWindow.NativeHandle;

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

    private static IReadOnlyList<IntPtr>
        GetCurrentProcessTopLevelWindows()
    {
        uint currentProcessId =
            (uint)Environment.ProcessId;

        List<IntPtr> windows = [];

        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(
                window,
                out uint processId);

            if (processId == currentProcessId &&
                IsWindowVisible(window))
            {
                windows.Add(window);
            }

            return true;
        }, IntPtr.Zero);

        return windows;
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

    private delegate bool EnumWindowsCallback(
        IntPtr window,
        IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(
        EnumWindowsCallback callback,
        IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(
        IntPtr window,
        out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(
        IntPtr window);

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
