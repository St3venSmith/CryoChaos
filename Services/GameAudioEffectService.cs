using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wasapi.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace CryoChaos.Services;

public enum GameAudioEffectMode
{
    Echo,
    Reverse,
    Radio,
    Underwater,
    PitchUp,
    PitchDown,
    Reverb,
    RandomStatic
}

/// <summary>
/// Captures only Destiny's process tree with WASAPI process loopback, applies
/// real-time DSP, and renders a louder wet signal over a quieter dry signal.
/// It never injects code into or reads memory from the game.
/// </summary>
public sealed class GameAudioEffectService : IDisposable
{
    private const float DryVolume = 0.22f;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public async Task PlayAsync(
        GameAudioEffectMode mode,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);

        AudioSessionVolumeLease? volumeLease = null;
        ProcessLoopbackCapture? capture = null;
        WasapiOut? output = null;
        try
        {
            using Process destiny = Process.GetProcessesByName("destiny2").FirstOrDefault()
                ?? throw new InvalidOperationException("Destiny 2 is not running.");

            // Process-loopback activation and its IAudioClient must originate
            // from a thread with a synchronization context (WPF's UI thread).
            SynchronizationContext context = SynchronizationContext.Current
                ?? throw new InvalidOperationException(
                    "Game audio effects must be started from the WPF UI thread.");

            capture = await ProcessLoopbackCapture.CreateAsync(
                destiny.Id,
                context,
                cancellationToken);

            BufferedWaveProvider buffer = new(capture.WaveFormat)
            {
                BufferDuration = TimeSpan.FromMilliseconds(500),
                DiscardOnBufferOverflow = true,
                ReadFully = true
            };
            AudioDspProcessor processor = new(mode, capture.WaveFormat);

            capture.DataAvailable += (_, data) =>
            {
                byte[] effected = processor.Process(data);
                buffer.AddSamples(effected, 0, effected.Length);
            };

            output = new WasapiOut(
                AudioClientShareMode.Shared,
                useEventSync: true,
                latency: 35);
            output.Init(buffer);

            // Start capture before lowering the session. On systems where the
            // process tap is post-volume, WetGain compensates for DryVolume.
            capture.Start();
            output.Play();
            volumeLease = AudioSessionVolumeLease.Lower(destiny.Id, DryVolume);

            await Task.Delay(duration, cancellationToken);
        }
        finally
        {
            // Restore the user's exact original mixer setting first, even when
            // cancellation, device removal, or DSP code throws.
            volumeLease?.Dispose();
            output?.Stop();
            capture?.Dispose();
            output?.Dispose();
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private sealed class AudioSessionVolumeLease : IDisposable
    {
        private readonly List<(SimpleAudioVolume Volume, float Original)> _sessions;
        private readonly MMDeviceEnumerator _devices;
        private readonly MMDevice _endpoint;
        private bool _restored;

        private AudioSessionVolumeLease(
            List<(SimpleAudioVolume Volume, float Original)> sessions,
            MMDeviceEnumerator devices,
            MMDevice endpoint)
        {
            _sessions = sessions;
            _devices = devices;
            _endpoint = endpoint;
        }

        public static AudioSessionVolumeLease Lower(int processId, float volume)
        {
            List<(SimpleAudioVolume, float)> changed = [];
            MMDeviceEnumerator devices = new();
            MMDevice endpoint = devices.GetDefaultAudioEndpoint(
                DataFlow.Render,
                Role.Multimedia);

            SessionCollection sessions = endpoint.AudioSessionManager.Sessions;
            for (int index = 0; index < sessions.Count; index++)
            {
                AudioSessionControl session = sessions[index];
                if (session.GetProcessID != (uint)processId)
                {
                    continue;
                }

                SimpleAudioVolume sessionVolume = session.SimpleAudioVolume;
                float original = sessionVolume.Volume;
                changed.Add((sessionVolume, original));
                sessionVolume.Volume = Math.Min(original, volume);
            }

            if (changed.Count == 0)
            {
                endpoint.Dispose();
                devices.Dispose();
                throw new InvalidOperationException(
                    "Destiny's Windows audio session was not found. Make sure the game is producing sound.");
            }

            return new AudioSessionVolumeLease(changed, devices, endpoint);
        }

        public void Dispose()
        {
            if (_restored)
            {
                return;
            }

            _restored = true;
            foreach ((SimpleAudioVolume volume, float original) in _sessions)
            {
                try
                {
                    volume.Volume = original;
                }
                catch (COMException)
                {
                    // The game or audio endpoint disappeared. There is no live
                    // session left whose volume could remain lowered.
                }
            }

            _endpoint.Dispose();
            _devices.Dispose();
        }
    }
}

internal sealed class ProcessLoopbackCapture : IDisposable
{
    private const string ProcessLoopbackDevice = "VAD\\Process_Loopback";
    private readonly AudioClient _client;
    private readonly AudioCaptureClient _capture;
    private readonly EventWaitHandle _sampleReady;
    private readonly SynchronizationContext _context;
    private readonly object _sync = new();
    private CancellationTokenSource? _pumpCancellation;
    private Task? _pump;
    private int _drainQueued;
    private bool _disposed;

    private ProcessLoopbackCapture(AudioClient client, SynchronizationContext context)
    {
        _client = client;
        _context = context;
        WaveFormat = new WaveFormat(48000, 16, 2);
        _sampleReady = new EventWaitHandle(false, EventResetMode.AutoReset);

        _client.Initialize(
            AudioClientShareMode.Shared,
            AudioClientStreamFlags.Loopback |
            AudioClientStreamFlags.EventCallback |
            AudioClientStreamFlags.AutoConvertPcm,
            0,
            0,
            WaveFormat,
            Guid.Empty);
        _client.SetEventHandle(_sampleReady.SafeWaitHandle.DangerousGetHandle());
        _capture = _client.AudioCaptureClient;
    }

    public WaveFormat WaveFormat { get; }
    public event EventHandler<byte[]>? DataAvailable;

    public static async Task<ProcessLoopbackCapture> CreateAsync(
        int processId,
        SynchronizationContext context,
        CancellationToken cancellationToken)
    {
        AudioClientActivationParams parameters = new()
        {
            ActivationType = 1,
            ProcessLoopbackParams = new AudioClientProcessLoopbackParams
            {
                TargetProcessId = (uint)processId,
                ProcessLoopbackMode = 0
            }
        };

        GCHandle parameterHandle = GCHandle.Alloc(parameters, GCHandleType.Pinned);
        try
        {
            PropVariant variant = new()
            {
                VariantType = (ushort)VarEnum.VT_BLOB,
                Blob = new Blob
                {
                    Length = Marshal.SizeOf<AudioClientActivationParams>(),
                    Data = parameterHandle.AddrOfPinnedObject()
                }
            };

            GCHandle variantHandle = GCHandle.Alloc(variant, GCHandleType.Pinned);
            try
            {
                ActivationHandler handler = new();
                Guid iid = typeof(IAudioClient).GUID;
                int hr = ActivateAudioInterfaceAsync(
                    ProcessLoopbackDevice,
                    ref iid,
                    variantHandle.AddrOfPinnedObject(),
                    handler,
                    out IActivateAudioInterfaceAsyncOperation operation);
                Marshal.ThrowExceptionForHR(hr);

                using CancellationTokenRegistration registration =
                    cancellationToken.Register(handler.Cancel);
                IntPtr pointer = await handler.Completion.WaitAsync(cancellationToken);
                try
                {
                    IAudioClient audioInterface =
                        (IAudioClient)Marshal.GetTypedObjectForIUnknown(
                            pointer,
                            typeof(IAudioClient));
                    return new ProcessLoopbackCapture(
                        new AudioClient(audioInterface),
                        context);
                }
                finally
                {
                    Marshal.Release(pointer);
                    Marshal.ReleaseComObject(operation);
                }
            }
            finally
            {
                variantHandle.Free();
            }
        }
        finally
        {
            parameterHandle.Free();
        }
    }

    public void Start()
    {
        _client.Start();
        _pumpCancellation = new CancellationTokenSource();
        _pump = Task.Run(() => Pump(_pumpCancellation.Token));
    }

    private void Pump(CancellationToken cancellationToken)
    {
        WaitHandle[] waits = [_sampleReady, cancellationToken.WaitHandle];
        while (WaitHandle.WaitAny(waits) == 0 && !cancellationToken.IsCancellationRequested)
        {
            if (Interlocked.Exchange(ref _drainQueued, 1) == 0)
            {
                _context.Post(_ =>
                {
                    try
                    {
                        if (!_disposed)
                        {
                            DrainPackets();
                        }
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _drainQueued, 0);
                    }
                }, null);
            }
        }
    }

    private void DrainPackets()
    {
        lock (_sync)
        {
            while (_capture.GetNextPacketSize() > 0)
            {
                IntPtr data = _capture.GetBuffer(
                    out int frameCount,
                    out AudioClientBufferFlags flags);
                int byteCount = frameCount * WaveFormat.BlockAlign;
                byte[] managed = new byte[byteCount];
                if (!flags.HasFlag(AudioClientBufferFlags.Silent))
                {
                    Marshal.Copy(data, managed, 0, byteCount);
                }

                _capture.ReleaseBuffer(frameCount);
                DataAvailable?.Invoke(this, managed);
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _pumpCancellation?.Cancel();
        _sampleReady.Set();
        try { _pump?.Wait(250); } catch (AggregateException) { }
        try { _client.Stop(); } catch (COMException) { }
        _pumpCancellation?.Dispose();
        _client.Dispose();
        _sampleReady.Dispose();
    }

    [DllImport("Mmdevapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int ActivateAudioInterfaceAsync(
        string deviceInterfacePath,
        ref Guid riid,
        IntPtr activationParams,
        IActivateAudioInterfaceCompletionHandler completionHandler,
        out IActivateAudioInterfaceAsyncOperation activationOperation);

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct AudioClientActivationParams
    {
        public int ActivationType;
        public AudioClientProcessLoopbackParams ProcessLoopbackParams;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct AudioClientProcessLoopbackParams
    {
        public uint TargetProcessId;
        public int ProcessLoopbackMode;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Blob
    {
        public int Length;
        public IntPtr Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort VariantType;
        [FieldOffset(8)] public Blob Blob;
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class ActivationHandler :
        IActivateAudioInterfaceCompletionHandler,
        IAgileObject
    {
        private readonly TaskCompletionSource<IntPtr> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<IntPtr> Completion => _completion.Task;

        public void ActivateCompleted(IActivateAudioInterfaceAsyncOperation operation)
        {
            operation.GetActivateResult(out int result, out IntPtr pointer);
            if (result < 0)
            {
                if (pointer != IntPtr.Zero) Marshal.Release(pointer);
                _completion.TrySetException(Marshal.GetExceptionForHR(result)!);
                return;
            }

            if (!_completion.TrySetResult(pointer) && pointer != IntPtr.Zero)
            {
                Marshal.Release(pointer);
            }
        }

        public void Cancel() => _completion.TrySetCanceled();
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90")]
    private interface IAgileObject
    {
    }
}
