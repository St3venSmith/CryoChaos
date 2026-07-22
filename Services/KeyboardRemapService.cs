using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using CryoChaos.Models;

namespace CryoChaos.Services;

public enum MouseLookDirection
{
    None,
    Up,
    Down
}

public enum MouseMovementMode
{
    BlockAll,
    InvertBothAxes
}

/// <summary>
/// Timed external input remapping/suppression using standard Windows low-level
/// hooks. Hooks act only while Destiny owns the foreground window.
/// </summary>
public sealed class KeyboardRemapService : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WhMouseLl = 14;
    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;
    private const int WmLeftButtonDown = 0x0201;
    private const int WmLeftButtonUp = 0x0202;
    private const int WmRightButtonDown = 0x0204;
    private const int WmRightButtonUp = 0x0205;
    private const int WmMiddleButtonDown = 0x0207;
    private const int WmMiddleButtonUp = 0x0208;
    private const int WmMouseWheel = 0x020A;
    private const int WmXButtonDown = 0x020B;
    private const int WmXButtonUp = 0x020C;
    private const int WmMouseMove = 0x0200;

    private readonly Dispatcher _dispatcher;
    private readonly LowLevelKeyboardProcedure _keyboardProcedure;
    private readonly LowLevelMouseProcedure _mouseProcedure;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private readonly Dictionary<Guid, IReadOnlyDictionary<ushort, ushort>>
        _keyboardRemapSessions = [];
    private readonly Dictionary<Guid, IReadOnlyDictionary<MouseInputButton, MouseInputButton>>
        _mouseRemapSessions = [];
    private readonly Dictionary<Guid, IReadOnlyList<InputBinding>>
        _suppressionSessions = [];
    private readonly Dictionary<Guid, MouseLookDirection>
        _lookSuppressionSessions = [];
    private readonly Dictionary<Guid, MouseMovementMode>
        _mouseMovementSessions = [];
    private IntPtr _destinyWindow;
    private NativePoint _lastMousePoint;
    private bool _hasLastMousePoint;
    private bool _disposed;

    public KeyboardRemapService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _keyboardProcedure = KeyboardHookProcedure;
        _mouseProcedure = MouseHookProcedure;
    }

    public async Task SwapAsync(
        InputBinding first,
        InputBinding second,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (first.Kind != InputBindingKind.Keyboard ||
            second.Kind != InputBindingKind.Keyboard)
        {
            throw new InvalidOperationException(
                "Input swapping currently supports keyboard bindings only.");
        }

        await RunTimedHookAsync(
            () => StartKeyboardRemap(
                new Dictionary<ushort, ushort>
                {
                    [first.VirtualKey] = second.VirtualKey,
                    [second.VirtualKey] = first.VirtualKey
                }),
            duration,
            cancellationToken);
    }

    public async Task RemapKeyboardAsync(
        IReadOnlyDictionary<ushort, ushort> mapping,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        Dictionary<ushort, ushort> usable = mapping
            .Where(pair => pair.Key != 0 && pair.Value != 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        if (usable.Count == 0)
        {
            throw new InvalidOperationException(
                "Keyboard remapping requires at least one valid key mapping.");
        }

        await RunTimedHookAsync(
            () => StartKeyboardRemap(usable),
            duration,
            cancellationToken);
    }

    public async Task SwapMouseButtonsAsync(
        MouseInputButton first,
        MouseInputButton second,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (first == MouseInputButton.None ||
            second == MouseInputButton.None ||
            first == second)
        {
            throw new InvalidOperationException(
                "Mouse remapping requires two different mouse buttons.");
        }

        await RunTimedHookAsync(
            () => StartMouseRemap(
                new Dictionary<MouseInputButton, MouseInputButton>
                {
                    [first] = second,
                    [second] = first
                }),
            duration,
            cancellationToken);
    }

    public async Task SuppressAsync(
        InputBinding binding,
        TimeSpan duration,
        CancellationToken cancellationToken) =>
        await SuppressAsync([binding], duration, cancellationToken);

    public async Task SuppressAsync(
        IReadOnlyCollection<InputBinding> bindings,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        InputBinding[] usable = bindings
            .Where(binding =>
                binding.Kind is InputBindingKind.Keyboard or
                InputBindingKind.MouseButton or
                InputBindingKind.MouseWheel)
            .DistinctBy(binding => new
            {
                binding.Kind,
                binding.VirtualKey,
                binding.MouseButton,
                binding.WheelDirection
            })
            .ToArray();

        if (usable.Length == 0)
        {
            throw new InvalidOperationException(
                "Input suppression requires a keyboard key, mouse button, or mouse wheel binding.");
        }

        await RunTimedHookAsync(
            () => StartSuppression(usable),
            duration,
            cancellationToken);
    }

    public async Task SuppressMouseDirectionAsync(
        MouseLookDirection direction,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (direction == MouseLookDirection.None)
        {
            throw new ArgumentOutOfRangeException(nameof(direction));
        }

        await RunTimedHookAsync(
            () => StartLookSuppression(direction),
            duration,
            cancellationToken);
    }

    public Task SuppressAllMouseInputsAsync(
        TimeSpan duration,
        CancellationToken cancellationToken) =>
        RunMouseMovementModeAsync(
            MouseMovementMode.BlockAll,
            duration,
            cancellationToken);

    public Task InvertMouseMovementAsync(
        TimeSpan duration,
        CancellationToken cancellationToken) =>
        RunMouseMovementModeAsync(
            MouseMovementMode.InvertBothAxes,
            duration,
            cancellationToken);

    private async Task RunMouseMovementModeAsync(
        MouseMovementMode mode,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await RunTimedHookAsync(
            () => StartMouseMovementMode(mode),
            duration,
            cancellationToken);
    }

    private async Task RunTimedHookAsync(
        Func<Guid> start,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        Guid sessionId = Guid.Empty;

        try
        {
            await _dispatcher.InvokeAsync(() => sessionId = start());
            await Task.Delay(duration, cancellationToken);
        }
        finally
        {
            if (!_dispatcher.HasShutdownStarted &&
                !_dispatcher.HasShutdownFinished)
            {
                await _dispatcher.InvokeAsync(() => RemoveSession(sessionId));
            }
        }
    }

    private Guid StartKeyboardRemap(
        IReadOnlyDictionary<ushort, ushort> mapping)
    {
        CacheDestinyWindow();
        Guid sessionId = Guid.NewGuid();

        foreach (ushort key in mapping.Keys.Concat(mapping.Values).Distinct())
        {
            KeyboardInputService.SendKeyboardState(key, pressed: false);
        }

        EnsureKeyboardHook();
        _keyboardRemapSessions.Add(sessionId, mapping);
        return sessionId;
    }

    private Guid StartMouseRemap(
        IReadOnlyDictionary<MouseInputButton, MouseInputButton> mapping)
    {
        CacheDestinyWindow();
        Guid sessionId = Guid.NewGuid();

        foreach (MouseInputButton button in
                 mapping.Keys.Concat(mapping.Values).Distinct())
        {
            KeyboardInputService.SendMouseButtonState(button, pressed: false);
        }

        EnsureMouseHook();
        _mouseRemapSessions.Add(sessionId, mapping);
        return sessionId;
    }

    private Guid StartSuppression(IReadOnlyList<InputBinding> bindings)
    {
        CacheDestinyWindow();
        Guid sessionId = Guid.NewGuid();

        foreach (InputBinding binding in bindings)
        {
            if (binding.Kind == InputBindingKind.Keyboard)
            {
                KeyboardInputService.SendKeyboardState(
                    binding.VirtualKey,
                    pressed: false);
            }
            else if (binding.Kind == InputBindingKind.MouseButton)
            {
                KeyboardInputService.SendMouseButtonState(
                    binding.MouseButton,
                    pressed: false);
            }
        }

        if (bindings.Any(binding => binding.Kind == InputBindingKind.Keyboard))
        {
            EnsureKeyboardHook();
        }

        if (bindings.Any(binding =>
                binding.Kind is InputBindingKind.MouseButton or
                InputBindingKind.MouseWheel))
        {
            EnsureMouseHook();
        }

        _suppressionSessions.Add(sessionId, bindings);
        return sessionId;
    }

    private Guid StartLookSuppression(MouseLookDirection direction)
    {
        CacheDestinyWindow();
        Guid sessionId = Guid.NewGuid();
        _hasLastMousePoint = false;
        EnsureMouseHook();
        _lookSuppressionSessions.Add(sessionId, direction);
        return sessionId;
    }

    private Guid StartMouseMovementMode(MouseMovementMode mode)
    {
        CacheDestinyWindow();
        Guid sessionId = Guid.NewGuid();

        if (mode == MouseMovementMode.BlockAll)
        {
            foreach (MouseInputButton button in Enum.GetValues<MouseInputButton>())
            {
                if (button != MouseInputButton.None)
                {
                    KeyboardInputService.SendMouseButtonState(
                        button,
                        pressed: false);
                }
            }
        }

        _hasLastMousePoint = false;
        EnsureMouseHook();
        _mouseMovementSessions.Add(sessionId, mode);
        return sessionId;
    }

    private void EnsureKeyboardHook() =>
        _keyboardHook = _keyboardHook == IntPtr.Zero
            ? InstallKeyboardHook()
            : _keyboardHook;

    private void EnsureMouseHook() =>
        _mouseHook = _mouseHook == IntPtr.Zero
            ? InstallMouseHook()
            : _mouseHook;

    private IntPtr InstallKeyboardHook()
    {
        IntPtr hook = SetWindowsKeyboardHook(
            WhKeyboardLl,
            _keyboardProcedure,
            GetModuleHandle(null),
            0);
        ThrowIfHookFailed(hook, "keyboard");
        return hook;
    }

    private IntPtr InstallMouseHook()
    {
        IntPtr hook = SetWindowsMouseHook(
            WhMouseLl,
            _mouseProcedure,
            GetModuleHandle(null),
            0);
        ThrowIfHookFailed(hook, "mouse");
        return hook;
    }

    private IntPtr KeyboardHookProcedure(
        int code,
        IntPtr wParam,
        IntPtr lParam)
    {
        try
        {
            if (code >= 0 &&
                ForegroundWindowService.IsWindowForeground(_destinyWindow))
            {
                KeyboardHookData data =
                    Marshal.PtrToStructure<KeyboardHookData>(lParam);
                ushort sourceKey = unchecked((ushort)data.VirtualKey);
                bool ownSynthetic =
                    data.ExtraInfo == KeyboardInputService.SyntheticInputMarker;

                if (TryGetPressed(wParam, out bool pressed))
                {
                    // Suppression includes events generated by keyboard
                    // software. Remapping skips only CryoChaos-tagged events,
                    // which prevents recursion without excluding peripheral
                    // software input.
                    if (IsKeyboardSuppressed(sourceKey))
                    {
                        return new IntPtr(1);
                    }

                    if (!ownSynthetic && _keyboardRemapSessions.Count > 0)
                    {
                        ushort targetKey = sourceKey;

                        foreach (IReadOnlyDictionary<ushort, ushort> mapping in
                                 _keyboardRemapSessions.Values)
                        {
                            if (mapping.TryGetValue(targetKey, out ushort next))
                            {
                                targetKey = next;
                            }
                        }

                        if (targetKey != sourceKey)
                        {
                            KeyboardInputService.SendKeyboardState(targetKey, pressed);
                            return new IntPtr(1);
                        }
                    }

                }
            }
        }
        catch (Exception exception)
        {
            CrashLogService.WriteException("KEYBOARD HOOK CALLBACK", exception);
        }

        return CallNextHookEx(_keyboardHook, code, wParam, lParam);
    }

    private IntPtr MouseHookProcedure(
        int code,
        IntPtr wParam,
        IntPtr lParam)
    {
        try
        {
            if (code >= 0 &&
                ForegroundWindowService.IsWindowForeground(_destinyWindow))
            {
                MouseHookData data =
                    Marshal.PtrToStructure<MouseHookData>(lParam);
                int message = wParam.ToInt32();
                bool ownSynthetic =
                    data.ExtraInfo == KeyboardInputService.SyntheticInputMarker;
                bool allMouseInputBlocked = IsAllMouseInputBlocked();

                if (allMouseInputBlocked &&
                    message != WmMouseMove &&
                    !ownSynthetic)
                {
                    return new IntPtr(1);
                }

                if (!ownSynthetic &&
                    _mouseRemapSessions.Count > 0 &&
                    TryGetMouseButtonState(
                        message,
                        data.MouseData,
                        out MouseInputButton sourceButton,
                        out bool pressed))
                {
                    MouseInputButton targetButton = sourceButton;

                    foreach (IReadOnlyDictionary<MouseInputButton, MouseInputButton> mapping in
                             _mouseRemapSessions.Values)
                    {
                        if (mapping.TryGetValue(targetButton, out MouseInputButton next))
                        {
                            targetButton = next;
                        }
                    }

                    if (targetButton != sourceButton)
                    {
                        KeyboardInputService.SendMouseButtonState(
                            targetButton,
                            pressed);
                        return new IntPtr(1);
                    }
                }

                if (IsMouseInputSuppressed(
                        message,
                        data.MouseData))
                {
                    return new IntPtr(1);
                }

                if (message == WmMouseMove)
                {
                    int deltaX = _hasLastMousePoint
                        ? data.Point.X - _lastMousePoint.X
                        : 0;
                    int deltaY = _hasLastMousePoint
                        ? data.Point.Y - _lastMousePoint.Y
                        : 0;

                    _lastMousePoint = data.Point;
                    _hasLastMousePoint = true;

                    // Destiny can consume physical movement through Raw Input,
                    // independently of whether this low-level message is
                    // suppressed. Counteract the observed physical delta and
                    // allow both movements to continue so their net movement
                    // is zero in consumers that accept SendInput movement.
                    // CryoChaos tags its injected event so it is never
                    // counteracted recursively.
                    if (allMouseInputBlocked)
                    {
                        if (!ownSynthetic &&
                            (deltaX != 0 || deltaY != 0))
                        {
                            KeyboardInputService.SendRelativeMouseMovement(
                                -deltaX,
                                -deltaY);
                        }

                        return CallNextHookEx(
                            _mouseHook,
                            code,
                            wParam,
                            lParam);
                    }

                    if (!ownSynthetic &&
                        ShouldInvertMouseMovement() &&
                        (deltaX != 0 || deltaY != 0))
                    {
                        KeyboardInputService.SendRelativeMouseMovement(
                            -deltaX,
                            -deltaY);
                        return new IntPtr(1);
                    }

                    if (IsLookDirectionSuppressed(deltaY))
                    {
                        return new IntPtr(1);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            CrashLogService.WriteException("MOUSE HOOK CALLBACK", exception);
        }

        return CallNextHookEx(_mouseHook, code, wParam, lParam);
    }

    private bool IsAllMouseInputBlocked()
    {
        foreach (MouseMovementMode mode in _mouseMovementSessions.Values)
        {
            if (mode == MouseMovementMode.BlockAll)
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldInvertMouseMovement()
    {
        int inversionCount = 0;

        foreach (MouseMovementMode mode in _mouseMovementSessions.Values)
        {
            if (mode == MouseMovementMode.InvertBothAxes)
            {
                inversionCount++;
            }
        }

        return inversionCount % 2 != 0;
    }

    private bool IsKeyboardSuppressed(ushort virtualKey)
    {
        foreach (IReadOnlyList<InputBinding> bindings in
                 _suppressionSessions.Values)
        {
            foreach (InputBinding binding in bindings)
            {
                if (binding.Kind == InputBindingKind.Keyboard &&
                    binding.VirtualKey == virtualKey)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsMouseInputSuppressed(int message, uint mouseData)
    {
        foreach (IReadOnlyList<InputBinding> bindings in
                 _suppressionSessions.Values)
        {
            foreach (InputBinding binding in bindings)
            {
                if (binding.Kind == InputBindingKind.MouseButton &&
                    IsSuppressedMouseMessage(
                        message,
                        mouseData,
                        binding.MouseButton))
                {
                    return true;
                }

                if (binding.Kind == InputBindingKind.MouseWheel &&
                    IsSuppressedMouseWheelMessage(
                        message,
                        mouseData,
                        binding.WheelDirection))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsLookDirectionSuppressed(int deltaY)
    {
        if (deltaY == 0)
        {
            return false;
        }

        MouseLookDirection movementDirection = deltaY < 0
            ? MouseLookDirection.Up
            : MouseLookDirection.Down;

        foreach (MouseLookDirection blockedDirection in
                 _lookSuppressionSessions.Values)
        {
            if (blockedDirection == movementDirection)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetPressed(IntPtr wParam, out bool pressed)
    {
        int message = wParam.ToInt32();
        if (message is WmKeyDown or WmSysKeyDown)
        {
            pressed = true;
            return true;
        }

        if (message is WmKeyUp or WmSysKeyUp)
        {
            pressed = false;
            return true;
        }

        pressed = false;
        return false;
    }

    private static bool IsSuppressedMouseMessage(
        int message,
        uint mouseData,
        MouseInputButton button)
    {
        return button switch
        {
            MouseInputButton.Left =>
                message is WmLeftButtonDown or WmLeftButtonUp,
            MouseInputButton.Right =>
                message is WmRightButtonDown or WmRightButtonUp,
            MouseInputButton.Middle =>
                message is WmMiddleButtonDown or WmMiddleButtonUp,
            MouseInputButton.XButton1 =>
                message is WmXButtonDown or WmXButtonUp &&
                ((mouseData >> 16) & 0xFFFF) == 1,
            MouseInputButton.XButton2 =>
                message is WmXButtonDown or WmXButtonUp &&
                ((mouseData >> 16) & 0xFFFF) == 2,
            _ => false
        };
    }

    private static bool IsSuppressedMouseWheelMessage(
        int message,
        uint mouseData,
        MouseWheelDirection direction)
    {
        if (message != WmMouseWheel)
        {
            return false;
        }

        short delta = unchecked((short)(mouseData >> 16));
        return direction switch
        {
            MouseWheelDirection.Up => delta > 0,
            MouseWheelDirection.Down => delta < 0,
            _ => false
        };
    }

    private static bool TryGetMouseButtonState(
        int message,
        uint mouseData,
        out MouseInputButton button,
        out bool pressed)
    {
        (button, pressed) = message switch
        {
            WmLeftButtonDown => (MouseInputButton.Left, true),
            WmLeftButtonUp => (MouseInputButton.Left, false),
            WmRightButtonDown => (MouseInputButton.Right, true),
            WmRightButtonUp => (MouseInputButton.Right, false),
            WmMiddleButtonDown => (MouseInputButton.Middle, true),
            WmMiddleButtonUp => (MouseInputButton.Middle, false),
            WmXButtonDown when ((mouseData >> 16) & 0xFFFF) == 1 =>
                (MouseInputButton.XButton1, true),
            WmXButtonUp when ((mouseData >> 16) & 0xFFFF) == 1 =>
                (MouseInputButton.XButton1, false),
            WmXButtonDown when ((mouseData >> 16) & 0xFFFF) == 2 =>
                (MouseInputButton.XButton2, true),
            WmXButtonUp when ((mouseData >> 16) & 0xFFFF) == 2 =>
                (MouseInputButton.XButton2, false),
            _ => (MouseInputButton.None, false)
        };

        return button != MouseInputButton.None;
    }

    private void RemoveSession(Guid sessionId)
    {
        if (sessionId == Guid.Empty)
        {
            return;
        }

        if (_keyboardRemapSessions.Remove(
                sessionId,
                out IReadOnlyDictionary<ushort, ushort>? keyboardMapping))
        {
            foreach (ushort key in keyboardMapping
                         .SelectMany(pair => new[] { pair.Key, pair.Value })
                         .Distinct())
            {
                KeyboardInputService.SendKeyboardState(key, pressed: false);
            }
        }

        if (_mouseRemapSessions.Remove(
                sessionId,
                out IReadOnlyDictionary<MouseInputButton, MouseInputButton>? mouseMapping))
        {
            foreach (MouseInputButton button in mouseMapping
                         .SelectMany(pair => new[] { pair.Key, pair.Value })
                         .Distinct())
            {
                KeyboardInputService.SendMouseButtonState(button, pressed: false);
            }
        }

        if (_suppressionSessions.Remove(
                sessionId,
                out IReadOnlyList<InputBinding>? suppressedBindings))
        {
            ReleaseBindings(suppressedBindings);
        }

        _lookSuppressionSessions.Remove(sessionId);
        if (_mouseMovementSessions.Remove(sessionId))
        {
            _hasLastMousePoint = false;
        }

        if (_keyboardHook != IntPtr.Zero &&
            _keyboardRemapSessions.Count == 0 &&
            !_suppressionSessions.Values
                .SelectMany(bindings => bindings)
                .Any(binding => binding.Kind == InputBindingKind.Keyboard))
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero &&
            _mouseRemapSessions.Count == 0 &&
            _lookSuppressionSessions.Count == 0 &&
            _mouseMovementSessions.Count == 0 &&
            !_suppressionSessions.Values
                .SelectMany(bindings => bindings)
                .Any(binding =>
                    binding.Kind is InputBindingKind.MouseButton or
                    InputBindingKind.MouseWheel))
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
            _hasLastMousePoint = false;
        }

        if (_keyboardHook == IntPtr.Zero && _mouseHook == IntPtr.Zero)
        {
            _destinyWindow = IntPtr.Zero;
        }
    }

    private static void ReleaseBindings(IEnumerable<InputBinding> bindings)
    {
        foreach (InputBinding binding in bindings)
        {
            if (binding.Kind == InputBindingKind.Keyboard)
            {
                KeyboardInputService.SendKeyboardState(
                    binding.VirtualKey,
                    pressed: false);
            }
            else if (binding.Kind == InputBindingKind.MouseButton)
            {
                KeyboardInputService.SendMouseButtonState(
                    binding.MouseButton,
                    pressed: false);
            }
        }
    }

    private void StopHooks()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
        }

        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = IntPtr.Zero;
        }

        foreach (ushort key in _keyboardRemapSessions.Values
                     .SelectMany(mapping => mapping)
                     .SelectMany(pair => new[] { pair.Key, pair.Value })
                     .Distinct())
        {
            KeyboardInputService.SendKeyboardState(key, pressed: false);
        }

        foreach (MouseInputButton button in _mouseRemapSessions.Values
                     .SelectMany(mapping => mapping)
                     .SelectMany(pair => new[] { pair.Key, pair.Value })
                     .Distinct())
        {
            KeyboardInputService.SendMouseButtonState(button, pressed: false);
        }

        ReleaseBindings(
            _suppressionSessions.Values.SelectMany(bindings => bindings));

        _keyboardRemapSessions.Clear();
        _mouseRemapSessions.Clear();
        _suppressionSessions.Clear();
        _lookSuppressionSessions.Clear();
        _mouseMovementSessions.Clear();
        _destinyWindow = IntPtr.Zero;
        _hasLastMousePoint = false;
    }

    private void CacheDestinyWindow()
    {
        IntPtr destinyWindow = DestinyWindowService.FindDestinyWindow();
        if (!DestinyWindowService.IsUsableWindow(destinyWindow))
        {
            throw new InvalidOperationException(
                "Destiny 2 must be running and not minimized before an input hook can start.");
        }

        _destinyWindow = destinyWindow;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(KeyboardRemapService));
    }

    private static void ThrowIfHookFailed(IntPtr hook, string kind)
    {
        if (hook != IntPtr.Zero) return;
        int error = Marshal.GetLastWin32Error();
        throw new Win32Exception(
            error,
            $"The {kind} input hook could not be installed (Windows error {error}).");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_dispatcher.CheckAccess())
            StopHooks();
        else if (!_dispatcher.HasShutdownStarted && !_dispatcher.HasShutdownFinished)
            _dispatcher.Invoke(StopHooks);

        GC.KeepAlive(_keyboardProcedure);
        GC.KeepAlive(_mouseProcedure);
    }

    private delegate IntPtr LowLevelKeyboardProcedure(
        int code, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr LowLevelMouseProcedure(
        int code, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardHookData
    {
        public uint VirtualKey;
        public uint ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookData
    {
        public NativePoint Point;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern IntPtr SetWindowsKeyboardHook(
        int hookId, LowLevelKeyboardProcedure procedure, IntPtr module, uint threadId);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern IntPtr SetWindowsMouseHook(
        int hookId, LowLevelMouseProcedure procedure, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(
        IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? moduleName);
}
