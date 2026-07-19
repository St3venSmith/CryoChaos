using System.Runtime.InteropServices;
using System.Windows.Interop;
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

    private ScreenTransformWindow? _transformWindow;
    private bool _disposed;

    public ScreenTransformService(OverlayWindow overlay)
    {
        _overlay = overlay;
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
            throw new ObjectDisposedException(nameof(ScreenTransformService));
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

                window.Show();
                BringOverlayAboveTransform();
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
        ScreenTransformWindow? window;

        lock (_stateLock)
        {
            window = _transformWindow;
            _transformWindow = null;
        }

        if (window is not null)
        {
            window.Close();
        }
    }

    private void BringOverlayAboveTransform()
    {
        IntPtr overlayHandle =
            new WindowInteropHelper(_overlay).Handle;

        if (overlayHandle == IntPtr.Zero)
        {
            return;
        }

        SetWindowPos(
            overlayHandle,
            HwndTopmost,
            0,
            0,
            0,
            0,
            SwpNoMove |
            SwpNoSize |
            SwpNoActivate |
            SwpShowWindow);
    }


    private static IReadOnlyList<IntPtr> GetCurrentProcessTopLevelWindows()
    {
        uint currentProcessId = (uint)Environment.ProcessId;
        List<IntPtr> windows = [];

        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out uint processId);

            if (processId == currentProcessId && IsWindowVisible(window))
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
    private static extern bool IsWindowVisible(IntPtr window);

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
