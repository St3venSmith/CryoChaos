using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace CryoChaos.Services;

public sealed record VideoOverlayOptions(
    string Path,
    double Opacity,
    double Volume,
    Stretch Stretch,
    bool Loop,
    TimeSpan MaximumDuration);

public sealed class VideoOverlayService : IDisposable
{
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExToolwindow = 0x80;
    private const int WsExNoactivate = 0x08000000;
    private readonly Dispatcher _dispatcher;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Window? _window;
    private MediaElement? _media;

    public VideoOverlayService(Dispatcher dispatcher) => _dispatcher = dispatcher;

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
        _media = new MediaElement
        {
            Source = new Uri(fullPath),
            LoadedBehavior = MediaState.Manual,
            UnloadedBehavior = MediaState.Manual,
            Stretch = options.Stretch,
            Volume = Math.Clamp(options.Volume, 0, 1),
            Opacity = Math.Clamp(options.Opacity, 0.05, 1),
            IsHitTestVisible = false
        };
        _media.MediaEnded += (_, _) =>
        {
            if (options.Loop)
            {
                _media.Position = TimeSpan.Zero;
                _media.Play();
            }
            else
            {
                playbackEnded.TrySetResult();
            }
        };
        _media.MediaFailed += (_, args) =>
            playbackEnded.TrySetException(
                args.ErrorException ?? new InvalidOperationException("Video playback failed."));

        Grid transparentSurface = new()
        {
            Background = Brushes.Transparent,
            IsHitTestVisible = false
        };
        transparentSurface.Children.Add(_media);

        _window = new Window
        {
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Topmost = true,
            WindowState = WindowState.Maximized,
            Content = transparentSurface,
            IsHitTestVisible = false
        };
        _window.SourceInitialized += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(_window).Handle;
            int style = GetWindowLong(hwnd, GwlExstyle);
            SetWindowLong(hwnd, GwlExstyle,
                style | WsExTransparent | WsExToolwindow | WsExNoactivate);
        };
        _window.Show();
        _media.Play();
    }

    private void CloseWindow()
    {
        if (_dispatcher.HasShutdownStarted || _dispatcher.HasShutdownFinished)
        {
            return;
        }

        _dispatcher.Invoke(() =>
        {
            _media?.Stop();
            _media?.Close();
            _window?.Close();
            _media = null;
            _window = null;
        });
    }

    public void Dispose()
    {
        CloseWindow();
        _gate.Dispose();
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
