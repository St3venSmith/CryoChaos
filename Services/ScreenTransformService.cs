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
    private const int MaximumSimultaneousScreenEffects = 2;
    private readonly SemaphoreSlim _effectSlots = new(
        MaximumSimultaneousScreenEffects,
        MaximumSimultaneousScreenEffects);
    private readonly object _stateLock = new();
    private readonly Dictionary<Guid, ScreenTransformMode> _activeModes = [];
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

        await _effectSlots.WaitAsync(cancellationToken);
        Guid activationId = Guid.NewGuid();

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
                lock (_stateLock)
                {
                    _activeModes[activationId] = mode;
                }

                ScreenTransformMode[] activeModes = GetActiveModes();
                if (_transformWindow is null)
                {
                    CaptureDiagnosticWindow window = new(
                        destinyWindow,
                        activeModes);
                    lock (_stateLock)
                    {
                        _transformWindow = window;
                    }

                    window.Show();
                    ForegroundWindowService.TryActivateDestinyWindow();
                    _zOrderTimer.Start();
                }
                else
                {
                    _transformWindow.SetEffectModes(activeModes);
                }

                EnsureOverlayAboveTransform();
            });

            try
            {
                await Task.Delay(duration, cancellationToken);
            }
            finally
            {
                await RemoveEffectAsync(activationId);
            }
        }
        finally
        {
            _effectSlots.Release();
        }
    }

    public async Task StopAsync()
    {
        if (_overlay.Dispatcher.HasShutdownStarted ||
            _overlay.Dispatcher.HasShutdownFinished)
        {
            return;
        }

        await _overlay.Dispatcher.InvokeAsync(() =>
        {
            lock (_stateLock)
            {
                _activeModes.Clear();
            }
            CloseTransformWindowOnUiThread();
        });
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

    private ScreenTransformMode[] GetActiveModes()
    {
        lock (_stateLock)
        {
            return _activeModes.Values
                .Take(MaximumSimultaneousScreenEffects)
                .ToArray();
        }
    }

    private async Task RemoveEffectAsync(Guid activationId)
    {
        if (_overlay.Dispatcher.HasShutdownStarted ||
            _overlay.Dispatcher.HasShutdownFinished)
        {
            return;
        }

        await _overlay.Dispatcher.InvokeAsync(() =>
        {
            lock (_stateLock)
            {
                _activeModes.Remove(activationId);
            }

            ScreenTransformMode[] remaining = GetActiveModes();
            if (remaining.Length == 0)
            {
                CloseTransformWindowOnUiThread();
            }
            else
            {
                _transformWindow?.SetEffectModes(remaining);
                EnsureOverlayAboveTransform();
            }
        });
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

        IntPtr destinyHandle = DestinyWindowService.FindDestinyWindow();
        if (!DestinyWindowService.IsUsableWindow(destinyHandle))
        {
            return;
        }

        // Place the transformed view immediately above Destiny but outside
        // the topmost group. Destiny remains the input owner, while every
        // CryoChaos HUD/popup/media topmost window can appear above the view.
        SetWindowPos(
            transformHandle,
            destinyHandle,
            0,
            0,
            0,
            0,
            flags);

        // Keep the normal CryoChaos HUD in the topmost group.
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

        _effectSlots.Dispose();
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
