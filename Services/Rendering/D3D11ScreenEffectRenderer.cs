using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.IO;
using System.Diagnostics;
using Vortice.D3DCompiler;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using Windows.Graphics;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;
using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Services.Rendering;

internal sealed unsafe class D3D11ScreenEffectRenderer : IDisposable
{
    private const uint BufferCount = 2;
    private const double MaximumRenderFps = 90.0;
    [StructLayout(LayoutKind.Sequential)]
    private struct PreviewSettings
    {
        public float Mode;
        public float Time;
        public float SourceWidth;
        public float SourceHeight;
    }

    private readonly object sync = new();
    private readonly IntPtr hwnd;

    private ID3D11Device device = null!;
    private ID3D11DeviceContext context = null!;
    private IDXGIFactory2 factory = null!;
    private IDXGISwapChain1 swapChain = null!;
    private ID3D11RenderTargetView renderTarget = null!;
    private ID3D11VertexShader vertexShader = null!;
    private ID3D11PixelShader pixelShader = null!;
    private ID3D11SamplerState sampler = null!;
    private ID3D11Buffer settingsBuffer = null!;
    private readonly Dictionary<IntPtr, ID3D11ShaderResourceView> frameViews = new();

    private int sourceWidth;
    private int sourceHeight;
    private int outputWidth = 1;
    private int outputHeight = 1;
    private bool allowTearing;

    private IDirect3DDevice winRtDevice = null!;
    private GraphicsCaptureItem? captureItem;
    private Direct3D11CaptureFramePool? framePool;
    private GraphicsCaptureSession? session;
    private bool disposed;
    private bool captureFaulted;
    private ScreenTransformMode mode;
    private readonly Stopwatch effectClock = Stopwatch.StartNew();
    private readonly Stopwatch fpsClock = Stopwatch.StartNew();
    private int framesSinceFpsUpdate;
    private long presentedFrames;
    private double currentFps;
    private long previousFrameTimestamp;
    private double renderCredits = 1.0;

    public D3D11ScreenEffectRenderer(IntPtr hwnd)
    {
        this.hwnd = hwnd;
        CreateDevice();
        CreateSwapChain();
        CreateShaders();
        CreateBackBuffer();
    }

    public ScreenTransformMode Mode
    {
        get => mode;
        set
        {
            lock (sync)
            {
                mode = value;
                effectClock.Restart();
            }
        }
    }

    public double CurrentFps
    {
        get { lock (sync) return currentFps; }
    }

    public long PresentedFrames
    {
        get { lock (sync) return presentedFrames; }
    }

    public (int Width, int Height) CaptureSize
    {
        get { lock (sync) return (sourceWidth, sourceHeight); }
    }

    public (int Width, int Height) OutputSize
    {
        get { lock (sync) return (outputWidth, outputHeight); }
    }

    private void CreateDevice()
    {
        FeatureLevel[] levels =
        [
            FeatureLevel.Level_11_1,
            FeatureLevel.Level_11_0,
            FeatureLevel.Level_10_1,
            FeatureLevel.Level_10_0
        ];

        DeviceCreationFlags flags = DeviceCreationFlags.BgraSupport;
#if DEBUG
        if (SdkLayersAvailable()) flags |= DeviceCreationFlags.Debug;
#endif

        D3D11CreateDevice(
            IntPtr.Zero,
            DriverType.Hardware,
            flags,
            levels,
            out device,
            out _,
            out context).CheckError();

        factory = CreateDXGIFactory1<IDXGIFactory2>();

        using (IDXGIFactory5? factory5 = factory.QueryInterfaceOrNull<IDXGIFactory5>())
            allowTearing = factory5?.PresentAllowTearing == true;

        using IDXGIDevice dxgiDevice = device.QueryInterface<IDXGIDevice>();
        using (IDXGIDevice1? latencyDevice = device.QueryInterfaceOrNull<IDXGIDevice1>())
        {
            if (latencyDevice != null)
                latencyDevice.MaximumFrameLatency = 1;
        }
        winRtDevice = CaptureInterop.CreateWinRtDevice(dxgiDevice.NativePointer);
    }

    private void CreateSwapChain()
    {
        SwapChainDescription1 description = new()
        {
            Width = (uint)outputWidth,
            Height = (uint)outputHeight,
            Format = Format.B8G8R8A8_UNorm,
            Stereo = false,
            SampleDescription = SampleDescription.Default,
            BufferUsage = Usage.RenderTargetOutput,
            BufferCount = BufferCount,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = AlphaMode.Ignore,
            Flags = allowTearing ? SwapChainFlags.AllowTearing : SwapChainFlags.None
        };

        SwapChainFullscreenDescription fullscreen = new() { Windowed = true };
        swapChain = factory.CreateSwapChainForHwnd(device, hwnd, description, fullscreen);
        factory.MakeWindowAssociation(hwnd, WindowAssociationFlags.IgnoreAltEnter);
    }

    private void CreateShaders()
    {
        string shader = Path.Combine(AppContext.BaseDirectory, "Shaders", "ScreenEffects.hlsl");
        ShaderFlags flags = ShaderFlags.EnableStrictness | ShaderFlags.OptimizationLevel3;
        ReadOnlyMemory<byte> vs = Compiler.CompileFromFile(shader, "VSMain", "vs_5_0", flags);
        ReadOnlyMemory<byte> ps = Compiler.CompileFromFile(shader, "PSMain", "ps_5_0", flags);

        vertexShader = device.CreateVertexShader(vs.Span);
        pixelShader = device.CreatePixelShader(ps.Span);
        sampler = device.CreateSamplerState(SamplerDescription.LinearClamp);

        settingsBuffer = device.CreateBuffer(new BufferDescription
        {
            ByteWidth = 16,
            BindFlags = BindFlags.ConstantBuffer,
            Usage = ResourceUsage.Dynamic,
            CPUAccessFlags = CpuAccessFlags.Write
        });
    }

    private void CreateBackBuffer()
    {
        using ID3D11Texture2D backBuffer = swapChain.GetBuffer<ID3D11Texture2D>(0);
        renderTarget = device.CreateRenderTargetView(backBuffer);
    }

    public void Resize(int width, int height)
    {
        lock (sync)
        {
            if (disposed || width <= 0 || height <= 0 ||
                (width == outputWidth && height == outputHeight)) return;

            outputWidth = width;
            outputHeight = height;
            context.ClearState();
            renderTarget.Dispose();
            swapChain.ResizeBuffers(BufferCount, (uint)width, (uint)height,
                Format.B8G8R8A8_UNorm,
                allowTearing ? SwapChainFlags.AllowTearing : SwapChainFlags.None).CheckError();
            CreateBackBuffer();
        }
    }

    public void StartCapture(IntPtr targetHwnd, ScreenTransformMode effectMode)
    {
        lock (sync)
        {
            StartCaptureCore(
                CaptureInterop.CreateForWindow(targetHwnd),
                effectMode);
        }
    }

    public void StartMonitorCapture(
        IntPtr monitor,
        ScreenTransformMode effectMode)
    {
        lock (sync)
        {
            StartCaptureCore(
                CaptureInterop.CreateForMonitor(monitor),
                effectMode);
        }
    }

    private void StartCaptureCore(
        GraphicsCaptureItem item,
        ScreenTransformMode effectMode)
    {
        StopCaptureCore();
        mode = effectMode;
        effectClock.Restart();
        fpsClock.Restart();
        framesSinceFpsUpdate = 0;
        presentedFrames = 0;
        currentFps = 0;
        captureFaulted = false;
        previousFrameTimestamp = 0;
        renderCredits = 1.0;
        captureItem = item;
        sourceWidth = captureItem.Size.Width;
        sourceHeight = captureItem.Size.Height;

        framePool = Direct3D11CaptureFramePool.CreateFreeThreaded(
            winRtDevice,
            DirectXPixelFormat.B8G8R8A8UIntNormalized,
            (int)BufferCount,
            captureItem.Size);

        session = framePool.CreateCaptureSession(captureItem);
        session.IsCursorCaptureEnabled = false;
        session.MinUpdateInterval = TimeSpan.FromMilliseconds(1);
        framePool.FrameArrived += FrameArrived;
        captureItem.Closed += CaptureItemClosed;
        session.StartCapture();
    }

    public void StopCapture()
    {
        lock (sync) StopCaptureCore();
    }

    private void StopCaptureCore()
    {
        // Clear the fields before releasing WinRT wrappers. CaptureItem.Closed
        // and HwndHost teardown can arrive close together; detaching ownership
        // first makes repeated cleanup harmless.
        GraphicsCaptureSession? oldSession = session;
        Direct3D11CaptureFramePool? oldFramePool = framePool;
        GraphicsCaptureItem? oldCaptureItem = captureItem;

        captureItem = null;
        session = null;
        framePool = null;

        TryCleanup(
            "detach capture frame callback",
            () => oldFramePool!.FrameArrived -= FrameArrived,
            oldFramePool is not null);
        TryCleanup(
            "detach capture-item callback",
            () => oldCaptureItem!.Closed -= CaptureItemClosed,
            oldCaptureItem is not null);

        DisposeSafely(oldSession, "capture session");
        DisposeSafely(oldFramePool, "capture frame pool");
        ClearFrameViews();
    }

    private void ClearFrameViews()
    {
        foreach (ID3D11ShaderResourceView view in frameViews.Values)
            view.Dispose();
        frameViews.Clear();
    }

    private void CaptureItemClosed(GraphicsCaptureItem sender, object args) => StopCapture();

    private void FrameArrived(Direct3D11CaptureFramePool sender, object args)
    {
        try
        {
            FrameArrivedCore(sender, args);
        }
        catch (Exception exception)
        {
            lock (sync)
            {
                captureFaulted = true;
            }

            // WinRT invokes this handler through a native callback boundary.
            // Never let a managed exception escape that boundary.
            CrashLogService.WriteException(
                "DIRECT3D CAPTURE CALLBACK",
                exception);
        }
    }

    private void FrameArrivedCore(
        Direct3D11CaptureFramePool sender,
        object args)
    {
        lock (sync)
        {
            if (disposed || captureFaulted || framePool == null) return;

            SizeInt32 nextSize = default;
            bool recreatePool = false;

            using (Direct3D11CaptureFrame? frame = sender.TryGetNextFrame())
            {
                if (frame == null) return;
                nextSize = frame.ContentSize;
                if (nextSize.Width <= 0 || nextSize.Height <= 0) return;

                if (nextSize.Width != sourceWidth || nextSize.Height != sourceHeight)
                {
                    sourceWidth = nextSize.Width;
                    sourceHeight = nextSize.Height;
                    ClearFrameViews();
                    recreatePool = true;
                }
                else
                {
                    if (!ShouldRenderFrame())
                    {
                        return;
                    }

                    Guid textureGuid = new("6F15AAF2-D208-4E89-9AB4-489535D34F9C");
                    IntPtr texturePointer = CaptureInterop.GetDxgiInterface(frame.Surface, textureGuid);
                    using ID3D11Texture2D frameTexture = new(texturePointer);
                    if (!frameViews.TryGetValue(texturePointer, out ID3D11ShaderResourceView? frameView))
                    {
                        frameView = device.CreateShaderResourceView(frameTexture);
                        frameViews.Add(texturePointer, frameView);
                    }
                    Render(frameView);
                }
            }

            if (recreatePool && framePool != null)
            {
                framePool.Recreate(winRtDevice,
                    DirectXPixelFormat.B8G8R8A8UIntNormalized, (int)BufferCount, nextSize);
            }
        }
    }

    private bool ShouldRenderFrame()
    {
        long timestamp = Stopwatch.GetTimestamp();

        if (previousFrameTimestamp != 0)
        {
            double elapsedSeconds =
                (timestamp - previousFrameTimestamp) /
                (double)Stopwatch.Frequency;

            // Token-bucket pacing keeps the newest WGC frames and discards
            // excess arrivals. Unlike sleeping the capture callback, this
            // does not build an input-latency queue. Two credits allow a fast
            // recovery after a brief scheduling stall without an unlimited
            // burst of GPU work.
            renderCredits = Math.Min(
                2.0,
                renderCredits + elapsedSeconds * MaximumRenderFps);
        }

        previousFrameTimestamp = timestamp;

        if (renderCredits < 1.0)
        {
            return false;
        }

        renderCredits -= 1.0;
        return true;
    }

    private void Render(ID3D11ShaderResourceView frameView)
    {
        context.OMSetRenderTargets(renderTarget);
        context.ClearRenderTargetView(renderTarget, Colors.Black);

        bool quarterTurn = mode is ScreenTransformMode.Rotate90Clockwise or
            ScreenTransformMode.Rotate90CounterClockwise;
        float sourceAspect = quarterTurn
            ? (float)sourceHeight / sourceWidth
            : (float)sourceWidth / sourceHeight;
        float outputAspect = (float)outputWidth / outputHeight;
        float viewWidth = outputWidth;
        float viewHeight = outputHeight;
        float x = 0;
        float y = 0;

        if (outputAspect > sourceAspect)
        {
            viewWidth = outputHeight * sourceAspect;
            x = (outputWidth - viewWidth) * 0.5f;
        }
        else
        {
            viewHeight = outputWidth / sourceAspect;
            y = (outputHeight - viewHeight) * 0.5f;
        }

        context.RSSetViewport(new Viewport(x, y, viewWidth, viewHeight, 0, 1));

        PreviewSettings settings = new()
        {
            Mode = (float)mode,
            Time = (float)effectClock.Elapsed.TotalSeconds,
            SourceWidth = sourceWidth,
            SourceHeight = sourceHeight
        };
        MappedSubresource mapped = context.Map(settingsBuffer, MapMode.WriteDiscard);
        Unsafe.Copy(mapped.DataPointer.ToPointer(), ref settings);
        context.Unmap(settingsBuffer, 0);

        context.IASetInputLayout(null);
        context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
        context.VSSetShader(vertexShader);
        context.VSSetConstantBuffer(0, settingsBuffer);
        context.PSSetShader(pixelShader);
        // EffectSettings is consumed by PSMain. Binding the buffer only to
        // the vertex stage leaves EffectMode at zero in the pixel shader,
        // making every transform/effect look like an unchanged live copy.
        context.PSSetConstantBuffer(0, settingsBuffer);
        context.PSSetShaderResource(0, frameView);
        context.PSSetSampler(0, sampler);
        context.Draw(3, 0);
        context.PSSetShaderResource(0, null!);

        swapChain.Present(0, allowTearing ? PresentFlags.AllowTearing : PresentFlags.None);

        presentedFrames++;
        framesSinceFpsUpdate++;
        if (fpsClock.Elapsed.TotalSeconds >= 1.0)
        {
            currentFps = framesSinceFpsUpdate / fpsClock.Elapsed.TotalSeconds;
            framesSinceFpsUpdate = 0;
            fpsClock.Restart();
        }
    }

    public void Dispose()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;

            TryCleanup("stop capture", StopCaptureCore);

            // A Vortice wrapper can remain non-null after its native COM
            // pointer has already been released by capture teardown. Calling
            // ClearState or Flush on that wrapper throws from inside Vortice
            // and used to create misleading crash-log entries on every normal
            // effect cleanup.
            bool hasLiveContext =
                context is not null && context.NativePointer != IntPtr.Zero;
            TryCleanup(
                "clear D3D context",
                () => context.ClearState(),
                hasLiveContext);
            TryCleanup(
                "flush D3D context",
                () => context.Flush(),
                hasLiveContext);

            DisposeSafely(settingsBuffer, "settings buffer");
            DisposeSafely(sampler, "sampler");
            DisposeSafely(pixelShader, "pixel shader");
            DisposeSafely(vertexShader, "vertex shader");
            DisposeSafely(renderTarget, "render target");
            DisposeSafely(swapChain, "swap chain");
            DisposeSafely(factory, "DXGI factory");
            DisposeSafely(context, "D3D context");
            DisposeSafely(device, "D3D device");
            DisposeSafely(winRtDevice, "WinRT D3D device");
        }
    }

    private static void DisposeSafely(
        IDisposable? resource,
        string resourceName)
    {
        if (resource is null)
        {
            return;
        }

        try
        {
            resource.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Some WinRT capture wrappers share an underlying object
            // reference and can report that it was already released.
        }
        catch (Exception exception)
        {
            CrashLogService.WriteException(
                $"RENDERER CLEANUP: {resourceName}",
                exception);
        }
    }

    private static void TryCleanup(
        string operation,
        Action cleanup,
        bool shouldRun = true)
    {
        if (!shouldRun)
        {
            return;
        }

        try
        {
            cleanup();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception exception)
        {
            CrashLogService.WriteException(
                $"RENDERER CLEANUP: {operation}",
                exception);
        }
    }
}
