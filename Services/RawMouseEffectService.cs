using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CryoChaos.Services;

public enum RawMouseEffectMode
{
    CancelLookUp,
    CancelLookDown,
    CancelVertical,
    CancelHorizontal,
    InvertHorizontal,
    InvertVertical,
    InvertBoth,
    Scale
}

/// <summary>
/// Reads one physical mouse through Raw Input and applies bounded relative
/// SendInput corrections from a separate fixed-rate output pump. It never
/// injects input from inside WM_INPUT and never touches the game process.
/// </summary>
public sealed class RawMouseEffectService : IDisposable
{
    private const int WmInput = 0x00FF;
    private const int WmInputDeviceChange = 0x00FE;
    private const int WmHotkey = 0x0312;
    private const int GidcRemoval = 2;

    private const int EmergencyHotkeyId = 0x4352594F;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkF12 = 0x7B;

    private const uint RidInput = 0x10000003;
    private const uint RimTypeMouse = 0;
    private const uint RidevInputSink = 0x00000100;
    private const uint RidevDevNotify = 0x00002000;
    private const uint RidevRemove = 0x00000001;
    private const ushort MouseMoveAbsolute = 0x0001;

    private const uint InputMouse = 0;
    private const uint MouseEventMove = 0x0001;
    private const uint InjectionMarkerValue = 0x4352594F;
    private static readonly UIntPtr InjectionMarker =
        new(InjectionMarkerValue);

    private const int PumpIntervalMilliseconds = 8;
    private const int MaximumSelectedPacketsPerSecond = 12000;
    private const int MaximumInjectionCallsPerSecond = 300;
    private const int MaximumPendingMagnitude = 5000;
    private const int MaximumSingleRawDelta = 2500;
    private const int MaximumAdaptiveOutputPerTick = 2500;
    private const double AdaptiveBurstHeadroom = 1.20;
    private const double MaximumCorrectionDebt = 12000.0;

    private readonly Window _window;
    private readonly SemaphoreSlim _effectGate = new(1, 1);
    private readonly object _stateLock = new();
    private readonly object _rateLock = new();

    private HwndSource? _source;
    private IntPtr _windowHandle;
    private Timer? _pumpTimer;
    private TaskCompletionSource<Exception?>? _stopped;
    private IntPtr _selectedDevice;
    private int _initialized;
    private int _running;
    private int _pumpActive;
    private bool _disposed;

    private long _pendingX;
    private long _pendingY;
    private long _rateWindowStart = Environment.TickCount64;
    private int _selectedPacketsInWindow;
    private int _injectionsInWindow;
    private double _correctionDebtX;
    private double _correctionDebtY;

    private RawMouseEffectMode _mode;
    private double _multiplier = 1.0;
    private int _baseOutputLimit = 160;

    public RawMouseEffectService(Window window)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
    }

    public async Task RunAsync(
        RawMouseEffectMode mode,
        double multiplier,
        int baseOutputLimit,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        if (!double.IsFinite(multiplier) || multiplier is < 0.1 or > 4.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(multiplier),
                "Mouse sensitivity multiplier must be between 0.1 and 4.0.");
        }

        if (baseOutputLimit is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(
                nameof(baseOutputLimit),
                "Mouse correction output must be between 1 and 500.");
        }

        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromSeconds(30))
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                "Raw mouse effects must last between 1 and 30 seconds.");
        }

        await _effectGate.WaitAsync(cancellationToken);

        try
        {
            await _window.Dispatcher.InvokeAsync(EnsureInitialized);

            lock (_stateLock)
            {
                _mode = mode;
                _multiplier = multiplier;
                _baseOutputLimit = baseOutputLimit;
            }

            ResetState();
            _stopped = new TaskCompletionSource<Exception?>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            Interlocked.Exchange(ref _running, 1);
            _pumpTimer = new Timer(
                PumpOutput,
                null,
                PumpIntervalMilliseconds,
                PumpIntervalMilliseconds);

            Task delay = Task.Delay(duration, cancellationToken);
            Task<Exception?> stopped = _stopped.Task;
            Task completed = await Task.WhenAny(delay, stopped);

            if (completed == delay)
            {
                await delay;
            }
            else
            {
                Exception? failure = await stopped;
                if (failure is not null)
                {
                    throw failure;
                }
            }
        }
        finally
        {
            StopCurrentEffect();
            _effectGate.Release();
        }
    }

    private void EnsureInitialized()
    {
        ThrowIfDisposed();
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0)
        {
            return;
        }

        try
        {
            _windowHandle = new WindowInteropHelper(_window).EnsureHandle();
            _source = HwndSource.FromHwnd(_windowHandle) ??
                throw new InvalidOperationException(
                    "Could not obtain the CryoChaos window input source.");

            _source.AddHook(WindowProcedure);

            RawInputDevice device = new()
            {
                UsagePage = 0x01,
                Usage = 0x02,
                Flags = RidevInputSink | RidevDevNotify,
                TargetWindow = _windowHandle
            };

            if (!RegisterRawInputDevices(
                    [device],
                    1,
                    (uint)Marshal.SizeOf<RawInputDevice>()))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Could not register CryoChaos for Raw Input mouse data.");
            }

            if (!RegisterHotKey(
                    _windowHandle,
                    EmergencyHotkeyId,
                    ModControl | ModAlt | ModNoRepeat,
                    VkF12))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Could not register Ctrl+Alt+F12 as the raw-mouse emergency stop.");
            }
        }
        catch
        {
            UnregisterHotKey(_windowHandle, EmergencyHotkeyId);

            RawInputDevice remove = new()
            {
                UsagePage = 0x01,
                Usage = 0x02,
                Flags = RidevRemove,
                TargetWindow = IntPtr.Zero
            };
            RegisterRawInputDevices(
                [remove],
                1,
                (uint)Marshal.SizeOf<RawInputDevice>());

            Interlocked.Exchange(ref _initialized, 0);
            if (_source is not null)
            {
                _source.RemoveHook(WindowProcedure);
                _source = null;
            }

            throw;
        }
    }

    private IntPtr WindowProcedure(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == EmergencyHotkeyId)
        {
            StopCurrentEffect();
            handled = true;
            return IntPtr.Zero;
        }

        if (message == WmInputDeviceChange &&
            wParam.ToInt32() == GidcRemoval &&
            lParam == _selectedDevice)
        {
            _selectedDevice = IntPtr.Zero;
            StopWithFault("The selected physical mouse was disconnected.");
            return IntPtr.Zero;
        }

        if (message == WmInput)
        {
            ProcessRawInput(lParam);
        }

        return IntPtr.Zero;
    }

    private void ProcessRawInput(IntPtr rawInputHandle)
    {
        uint size = 0;
        uint headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        uint result = GetRawInputData(
            rawInputHandle,
            RidInput,
            IntPtr.Zero,
            ref size,
            headerSize);

        if (result == uint.MaxValue || size < headerSize)
        {
            return;
        }

        IntPtr buffer = Marshal.AllocHGlobal(checked((int)size));

        try
        {
            result = GetRawInputData(
                rawInputHandle,
                RidInput,
                buffer,
                ref size,
                headerSize);

            if (result == uint.MaxValue || result != size)
            {
                return;
            }

            RawInput raw = Marshal.PtrToStructure<RawInput>(buffer);
            if (raw.Header.Type != RimTypeMouse ||
                (raw.Mouse.Flags & MouseMoveAbsolute) != 0 ||
                raw.Header.Device == IntPtr.Zero ||
                raw.Mouse.ExtraInformation == InjectionMarkerValue ||
                Volatile.Read(ref _running) == 0)
            {
                return;
            }

            if (_selectedDevice == IntPtr.Zero)
            {
                // Automatically lock onto the first physical mouse moved
                // during an effect. The handle remains selected until that
                // device is disconnected.
                _selectedDevice = raw.Header.Device;
            }

            if (raw.Header.Device != _selectedDevice)
            {
                return;
            }

            int x = raw.Mouse.LastX;
            int y = raw.Mouse.LastY;
            if (x == 0 && y == 0)
            {
                return;
            }

            if (Math.Abs((long)x) > MaximumSingleRawDelta ||
                Math.Abs((long)y) > MaximumSingleRawDelta)
            {
                StopWithFault("Raw mouse movement exceeded the safety limit.");
                return;
            }

            if (!CheckSelectedPacketRate())
            {
                return;
            }

            long pendingX = Interlocked.Add(ref _pendingX, x);
            long pendingY = Interlocked.Add(ref _pendingY, y);

            if (Math.Abs(pendingX) > MaximumPendingMagnitude ||
                Math.Abs(pendingY) > MaximumPendingMagnitude)
            {
                StopWithFault("Raw mouse movement backlog exceeded the safety limit.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void PumpOutput(object? state)
    {
        if (Interlocked.Exchange(ref _pumpActive, 1) != 0)
        {
            return;
        }

        try
        {
            if (Volatile.Read(ref _running) == 0)
            {
                return;
            }

            int physicalX = ClampToInt(
                Interlocked.Exchange(ref _pendingX, 0));
            int physicalY = ClampToInt(
                Interlocked.Exchange(ref _pendingY, 0));

            RawMouseEffectMode mode;
            double multiplier;
            int baseOutputLimit;

            lock (_stateLock)
            {
                mode = _mode;
                multiplier = _multiplier;
                baseOutputLimit = _baseOutputLimit;
            }

            if (physicalX != 0 || physicalY != 0)
            {
                (double desiredX, double desiredY) =
                    CalculateDesiredMovement(
                        mode,
                        multiplier,
                        physicalX,
                        physicalY);

                _correctionDebtX += desiredX - physicalX;
                _correctionDebtY += desiredY - physicalY;

                if (Math.Abs(_correctionDebtX) > MaximumCorrectionDebt ||
                    Math.Abs(_correctionDebtY) > MaximumCorrectionDebt)
                {
                    StopWithFault("Mouse correction backlog exceeded the safety limit.");
                    return;
                }
            }

            if (Math.Abs(_correctionDebtX) < 0.5 &&
                Math.Abs(_correctionDebtY) < 0.5)
            {
                _correctionDebtX = 0;
                _correctionDebtY = 0;
                return;
            }

            int adaptiveLimit = CalculateAdaptiveOutputLimit(
                mode,
                baseOutputLimit,
                physicalX,
                physicalY,
                _correctionDebtX,
                _correctionDebtY);

            int injectedX = ClampToRange(
                _correctionDebtX,
                -adaptiveLimit,
                adaptiveLimit);
            int injectedY = ClampToRange(
                _correctionDebtY,
                -adaptiveLimit,
                adaptiveLimit);

            _correctionDebtX -= injectedX;
            _correctionDebtY -= injectedY;

            if ((injectedX == 0 && injectedY == 0) ||
                !CheckInjectionRate() ||
                Volatile.Read(ref _running) == 0)
            {
                return;
            }

            Input input = new()
            {
                Type = InputMouse,
                Data = new InputUnion
                {
                    Mouse = new MouseInput
                    {
                        X = injectedX,
                        Y = injectedY,
                        Flags = MouseEventMove,
                        ExtraInfo = InjectionMarker
                    }
                }
            };

            uint sent = SendInput(1, [input], Marshal.SizeOf<Input>());
            if (sent != 1)
            {
                StopWithFault(
                    $"Mouse correction failed with Windows error {Marshal.GetLastWin32Error()}.");
            }
        }
        catch (Exception exception)
        {
            CrashLogService.WriteException("RAW MOUSE OUTPUT PUMP", exception);
            StopWithFault(exception.Message);
        }
        finally
        {
            Volatile.Write(ref _pumpActive, 0);
        }
    }

    private static (double X, double Y) CalculateDesiredMovement(
        RawMouseEffectMode mode,
        double multiplier,
        int physicalX,
        int physicalY) =>
        mode switch
        {
            RawMouseEffectMode.CancelLookUp =>
                (physicalX, physicalY < 0 ? 0 : physicalY),
            RawMouseEffectMode.CancelLookDown =>
                (physicalX, physicalY > 0 ? 0 : physicalY),
            RawMouseEffectMode.CancelVertical =>
                (physicalX, 0),
            RawMouseEffectMode.CancelHorizontal =>
                (0, physicalY),
            RawMouseEffectMode.InvertHorizontal =>
                (-physicalX, physicalY),
            RawMouseEffectMode.InvertVertical =>
                (physicalX, -physicalY),
            RawMouseEffectMode.InvertBoth =>
                (-physicalX, -physicalY),
            RawMouseEffectMode.Scale =>
                (physicalX * multiplier, physicalY * multiplier),
            _ => (physicalX, physicalY)
        };

    private static int CalculateAdaptiveOutputLimit(
        RawMouseEffectMode mode,
        int baseOutputLimit,
        int physicalX,
        int physicalY,
        double correctionDebtX,
        double correctionDebtY)
    {
        if (mode == RawMouseEffectMode.Scale)
        {
            return baseOutputLimit;
        }

        double observedMagnitude = Math.Max(
            Math.Abs((double)physicalX),
            Math.Abs((double)physicalY));
        double debtMagnitude = Math.Max(
            Math.Abs(correctionDebtX),
            Math.Abs(correctionDebtY));
        double requested = Math.Max(
            baseOutputLimit,
            Math.Max(
                observedMagnitude * AdaptiveBurstHeadroom,
                debtMagnitude * 0.75));

        return (int)Math.Clamp(
            Math.Ceiling(requested),
            baseOutputLimit,
            MaximumAdaptiveOutputPerTick);
    }

    private bool CheckSelectedPacketRate()
    {
        lock (_rateLock)
        {
            ResetRateWindowIfNeeded();
            if (++_selectedPacketsInWindow <=
                MaximumSelectedPacketsPerSecond)
            {
                return true;
            }
        }

        StopWithFault("Raw mouse packet rate exceeded the safety limit.");
        return false;
    }

    private bool CheckInjectionRate()
    {
        lock (_rateLock)
        {
            ResetRateWindowIfNeeded();
            if (++_injectionsInWindow <= MaximumInjectionCallsPerSecond)
            {
                return true;
            }
        }

        StopWithFault("Mouse correction output rate exceeded the safety limit.");
        return false;
    }

    private void ResetRateWindowIfNeeded()
    {
        long now = Environment.TickCount64;
        if (now - _rateWindowStart < 1000)
        {
            return;
        }

        _rateWindowStart = now;
        _selectedPacketsInWindow = 0;
        _injectionsInWindow = 0;
    }

    private void ResetState()
    {
        Interlocked.Exchange(ref _pendingX, 0);
        Interlocked.Exchange(ref _pendingY, 0);
        Volatile.Write(ref _pumpActive, 0);
        _correctionDebtX = 0;
        _correctionDebtY = 0;

        lock (_rateLock)
        {
            _rateWindowStart = Environment.TickCount64;
            _selectedPacketsInWindow = 0;
            _injectionsInWindow = 0;
        }
    }

    private void StopWithFault(string message) =>
        StopCurrentEffect(new InvalidOperationException(message));

    private void StopCurrentEffect(Exception? failure = null)
    {
        if (Interlocked.Exchange(ref _running, 0) == 0)
        {
            return;
        }

        Timer? timer = Interlocked.Exchange(ref _pumpTimer, null);
        timer?.Dispose();
        Interlocked.Exchange(ref _pendingX, 0);
        Interlocked.Exchange(ref _pendingY, 0);
        _correctionDebtX = 0;
        _correctionDebtY = 0;
        _stopped?.TrySetResult(failure);
    }

    private static int ClampToInt(long value)
    {
        if (value > int.MaxValue)
        {
            return int.MaxValue;
        }

        if (value < int.MinValue)
        {
            return int.MinValue;
        }

        return (int)value;
    }

    private static int ClampToRange(
        double value,
        int minimum,
        int maximum)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return (int)Math.Round(Math.Clamp(value, minimum, maximum));
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(RawMouseEffectService));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopCurrentEffect();

        void CleanupWindowResources()
        {
            if (Volatile.Read(ref _initialized) == 0)
            {
                return;
            }

            UnregisterHotKey(_windowHandle, EmergencyHotkeyId);

            RawInputDevice remove = new()
            {
                UsagePage = 0x01,
                Usage = 0x02,
                Flags = RidevRemove,
                TargetWindow = IntPtr.Zero
            };

            RegisterRawInputDevices(
                [remove],
                1,
                (uint)Marshal.SizeOf<RawInputDevice>());

            _source?.RemoveHook(WindowProcedure);
            _source = null;
            Interlocked.Exchange(ref _initialized, 0);
        }

        if (_window.Dispatcher.CheckAccess())
        {
            CleanupWindowResources();
        }
        else if (!_window.Dispatcher.HasShutdownStarted &&
                 !_window.Dispatcher.HasShutdownFinished)
        {
            _window.Dispatcher.Invoke(CleanupWindowResources);
        }

        // Do not dispose the semaphore here. An effect cancelled by window
        // shutdown may still be unwinding its finally block and releasing it.
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterRawInputDevices(
        [In] RawInputDevice[] devices,
        uint numberOfDevices,
        uint structureSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        IntPtr rawInput,
        uint command,
        IntPtr data,
        ref uint size,
        uint headerSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint numberOfInputs,
        [In] Input[] inputs,
        int structureSize);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(
        IntPtr window,
        int id,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(
        IntPtr window,
        int id);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr TargetWindow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr Parameter;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInput
    {
        public RawInputHeader Header;
        public RawMouse Mouse;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct RawMouse
    {
        [FieldOffset(0)] public ushort Flags;
        [FieldOffset(4)] public uint Buttons;
        [FieldOffset(4)] public ushort ButtonFlags;
        [FieldOffset(6)] public ushort ButtonData;
        [FieldOffset(8)] public uint RawButtons;
        [FieldOffset(12)] public int LastX;
        [FieldOffset(16)] public int LastY;
        [FieldOffset(20)] public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}
