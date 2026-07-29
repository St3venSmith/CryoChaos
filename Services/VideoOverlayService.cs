using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using VlcMedia = LibVLCSharp.Shared.Media;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace CryoChaos.Services;

public sealed record VideoOverlayOptions(
    string Path,
    double Opacity,
    double Volume,
    Stretch Stretch,
    bool Loop,
    TimeSpan MaximumDuration,
    bool TransparentBackground = true);

public sealed class VideoOverlayService : IDisposable
{
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExToolwindow = 0x80;
    private const int WsExNoactivate = 0x08000000;
    private readonly Dispatcher _dispatcher;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Window? _window;
    private VideoView? _videoView;
    private LibVLC? _libVlc;
    private VlcMedia? _media;
    private VlcMediaPlayer? _mediaPlayer;

    public VideoOverlayService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        Core.Initialize();
    }

    public async Task ShowAsync(
        VideoOverlayOptions options,
        CancellationToken cancellationToken)
    {
        string fullPath = System.IO.Path.GetFullPath(
            System.IO.Path.IsPathRooted(options.Path)
                ? options.Path
                : System.IO.Path.Combine(AppContext.BaseDirectory, options.Path));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("The overlay video was not found.", fullPath);
        }

        await _gate.WaitAsync(cancellationToken);
        TaskCompletionSource playbackEnded =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        try
        {
            _dispatcher.Invoke(() => CreateWindow(fullPath, options, playbackEnded));
            using CancellationTokenSource timeout =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task duration = Task.Delay(options.MaximumDuration, timeout.Token);
            Task completed = await Task.WhenAny(playbackEnded.Task, duration);
            timeout.Cancel();
            if (completed == playbackEnded.Task)
            {
                await playbackEnded.Task;
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
        finally
        {
            CloseWindow();
            _gate.Release();
        }
    }

    private void CreateWindow(
        string fullPath,
        VideoOverlayOptions options,
        TaskCompletionSource playbackEnded)
    {
        _libVlc = new LibVLC(
            "--no-video-title-show",
            "--no-osd",
            "--quiet");
        _mediaPlayer = new VlcMediaPlayer(_libVlc)
        {
            Volume = (int)Math.Round(
                Math.Clamp(options.Volume, 0, 1) * 100)
        };
        _media = new VlcMedia(
            _libVlc,
            new Uri(fullPath));
        // Software decoding avoids the black-frame hardware-video-overlay
        // path seen on some systems while Destiny is using the GPU.
        _media.AddOption(":avcodec-hw=none");
        _videoView = new VideoView
        {
            MediaPlayer = _mediaPlayer,
            IsHitTestVisible = false,
            Opacity = Math.Clamp(options.Opacity, 0.05, 1)
        };

        _mediaPlayer.EndReached += (_, _) =>
        {
            if (options.Loop)
            {
                // LibVLC events run on a native callback thread. Restart
                // outside that callback to avoid re-entering the player.
                _ = Task.Run(() =>
                {
                    if (_mediaPlayer is not null &&
                        _media is not null)
                    {
                        _mediaPlayer.Stop();
                        _mediaPlayer.Play(_media);
                    }
                });
            }
            else
            {
                playbackEnded.TrySetResult();
            }
        };
        _mediaPlayer.EncounteredError += (_, _) =>
        {
            CrashLogService.Write(
                "VIDEO",
                $"LibVLC reported a playback error for '{fullPath}'.");
            playbackEnded.TrySetException(
                new InvalidOperationException(
                    $"LibVLC could not play '{fullPath}'."));
        };

        _window = new Window
        {
            // LibVLC renders into a native child HWND. Full-screen videos use
            // an ordinary opaque borderless host while both HWNDs remain
            // click-through and non-activating.
            AllowsTransparency = options.TransparentBackground,
            Background = options.TransparentBackground
                ? Brushes.Transparent
                : Brushes.Black,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            WindowState = WindowState.Normal,
            Content = _videoView,
            IsHitTestVisible = false
        };
        _window.SourceInitialized += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(_window).Handle;
            MakeWindowClickThrough(hwnd);
            GameMonitorPlacementService.FillGameMonitor(
                _window,
                activate: false);
        };
        bool playbackStarted = false;
        _window.ContentRendered += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(_window).Handle;
            MakeWindowClickThrough(hwnd);
            EnumChildWindows(
                hwnd,
                (child, _) =>
                {
                    MakeWindowClickThrough(child);
                    return true;
                },
                IntPtr.Zero);

            // VideoView creates its native rendering HWND during layout.
            // Starting before ContentRendered can make LibVLC play into no
            // target at all, leaving only the black parent window visible.
            if (playbackStarted)
            {
                return;
            }

            playbackStarted = true;
            CrashLogService.Write(
                "VIDEO",
                $"Starting video overlay '{fullPath}'.");
            if (_mediaPlayer is null ||
                _media is null ||
                !_mediaPlayer.Play(_media))
            {
                playbackEnded.TrySetException(
                    new InvalidOperationException(
                        $"LibVLC refused to start '{fullPath}'."));
            }
        };
        _window.Show();
    }

    private void CloseWindow()
    {
        if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
        {
            return;
        }

        _dispatcher.Invoke(() =>
        {
            _mediaPlayer?.Stop();
            _window?.Close();

            if (_videoView is not null)
            {
                _videoView.MediaPlayer = null;
            }

            _mediaPlayer?.Dispose();
            _media?.Dispose();
            _libVlc?.Dispose();

            _videoView = null;
            _mediaPlayer = null;
            _media = null;
            _libVlc = null;
            _window = null;
        });
    }

    public void Dispose()
    {
        CloseWindow();
        _gate.Dispose();
    }

    private static void MakeWindowClickThrough(IntPtr window)
    {
        if (window == IntPtr.Zero)
        {
            return;
        }

        int style = GetWindowLong(window, GwlExstyle);
        SetWindowLong(
            window,
            GwlExstyle,
            style |
            WsExTransparent |
            WsExToolwindow |
            WsExNoactivate);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    private delegate bool EnumWindowProcedure(
        IntPtr window,
        IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumChildWindows(
        IntPtr parent,
        EnumWindowProcedure callback,
        IntPtr parameter);
}
