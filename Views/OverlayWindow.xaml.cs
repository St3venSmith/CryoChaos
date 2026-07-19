using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace CryoChaos.Views;

public partial class OverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly Dictionary<string, FrameworkElement> _activeElements =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, ActiveHudEffect> _activeHudEffects =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly DispatcherTimer _hudTimer;
    private bool _engineRunning;

    public OverlayWindow()
    {
        InitializeComponent();

        // Keep the effect surface on the primary game display. The HUD itself
        // uses fixed WPF device-independent pixel dimensions in XAML.
        Left = 0;
        Top = 0;
        Width = SystemParameters.PrimaryScreenWidth;
        Height = SystemParameters.PrimaryScreenHeight;

        _hudTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _hudTimer.Tick += (_, _) => RefreshCurrentEffectHud();
        _hudTimer.Start();

        SourceInitialized += (_, _) => MakeClickThrough();
        Closed += (_, _) => _hudTimer.Stop();
    }

    public void SetEngineRunning(bool running)
    {
        RunOnUi(() =>
        {
            _engineRunning = running;

            if (!running)
            {
                NextEffectProgressBar.Value = 0;
                NextEffectTextBlock.Text = "CHAOS STOPPED";
                _activeHudEffects.Clear();
                RefreshCurrentEffectHud();
            }
            else
            {
                NextEffectProgressBar.Value = 100;
                NextEffectTextBlock.Text = "CHOOSING NEXT EFFECT...";
            }
        });
    }

    public void UpdateNextEffectCountdown(TimeSpan remaining, TimeSpan total)
    {
        RunOnUi(() =>
        {
            if (!_engineRunning)
            {
                return;
            }

            double progress = total.TotalMilliseconds <= 0
                ? 0
                : remaining.TotalMilliseconds / total.TotalMilliseconds * 100.0;

            NextEffectProgressBar.Value = Math.Clamp(progress, 0, 100);
            NextEffectTextBlock.Text = $"NEXT EFFECT IN {FormatTime(remaining)}";
        });
    }

    public void AddActiveEffect(string effectId, string effectName, TimeSpan displayDuration)
    {
        RunOnUi(() =>
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            _activeHudEffects[effectId] = new ActiveHudEffect(
                effectName,
                now,
                now.Add(displayDuration));

            RefreshCurrentEffectHud();
        });
    }

    public void RemoveActiveEffect(string effectId)
    {
        RunOnUi(() =>
        {
            _activeHudEffects.Remove(effectId);
            RefreshCurrentEffectHud();
        });
    }

    public async Task ShowTintAsync(
        string effectId,
        Color color,
        double opacity,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        Rectangle rectangle = new()
        {
            Width = Width,
            Height = Height,
            Fill = new SolidColorBrush(color),
            Opacity = Math.Clamp(opacity, 0, 1),
            IsHitTestVisible = false
        };

        await AddForDurationAsync(effectId, rectangle, duration, cancellationToken);
    }

    public async Task ShowTunnelVisionAsync(
        string effectId,
        double openingScale,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        Grid layer = new()
        {
            Width = Width,
            Height = Height,
            IsHitTestVisible = false
        };

        openingScale = Math.Clamp(openingScale, 0.15, 0.9);
        double openingWidth = Width * openingScale;
        double openingHeight = Height * openingScale;
        double sideWidth = Math.Max(0, (Width - openingWidth) / 2);
        double topHeight = Math.Max(0, (Height - openingHeight) / 2);

        // Fully opaque black. Only the center opening remains transparent.
        Brush brush = Brushes.Black;

        layer.Children.Add(new Rectangle
        {
            Width = Width,
            Height = topHeight,
            Fill = brush,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        });

        layer.Children.Add(new Rectangle
        {
            Width = Width,
            Height = topHeight,
            Fill = brush,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Bottom
        });

        layer.Children.Add(new Rectangle
        {
            Width = sideWidth,
            Height = openingHeight,
            Fill = brush,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center
        });

        layer.Children.Add(new Rectangle
        {
            Width = sideWidth,
            Height = openingHeight,
            Fill = brush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        });

        await AddForDurationAsync(effectId, layer, duration, cancellationToken);
    }

    public async Task ShowBlackoutPulseAsync(
        string effectId,
        double peakOpacity,
        int pulseCount,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        Rectangle rectangle = new()
        {
            Width = Width,
            Height = Height,
            Fill = Brushes.Black,
            Opacity = 0,
            IsHitTestVisible = false
        };

        await Dispatcher.InvokeAsync(() => AddElement(effectId, rectangle));

        try
        {
            pulseCount = Math.Max(1, pulseCount);
            peakOpacity = Math.Clamp(peakOpacity, 0, 1);

            TimeSpan pulseWindow = TimeSpan.FromMilliseconds(
                duration.TotalMilliseconds / pulseCount);

            TimeSpan fadeIn = TimeSpan.FromMilliseconds(
                Math.Clamp(pulseWindow.TotalMilliseconds * 0.14, 80, 150));

            TimeSpan fullBlackHold = TimeSpan.FromMilliseconds(
                Math.Clamp(pulseWindow.TotalMilliseconds * 0.34, 220, 550));

            TimeSpan fadeOut = fadeIn;
            TimeSpan gap = pulseWindow - fadeIn - fullBlackHold - fadeOut;
            if (gap < TimeSpan.Zero)
            {
                gap = TimeSpan.Zero;
            }

            for (int i = 0; i < pulseCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                await AnimateOpacityAsync(rectangle, peakOpacity, fadeIn, cancellationToken);

                // Hold at opacity 1.0 so the blink becomes completely black
                // instead of only touching black for a single animation frame.
                rectangle.BeginAnimation(OpacityProperty, null);
                rectangle.Opacity = peakOpacity;
                await Task.Delay(fullBlackHold, cancellationToken);

                await AnimateOpacityAsync(rectangle, 0, fadeOut, cancellationToken);

                if (gap > TimeSpan.Zero && i < pulseCount - 1)
                {
                    await Task.Delay(gap, cancellationToken);
                }
            }
        }
        finally
        {
            await Dispatcher.InvokeAsync(() => RemoveElement(effectId));
        }
    }

    public async Task ShowMovingBlockAsync(
        string effectId,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        Rectangle rectangle = new()
        {
            Width = Math.Max(180, Width * 0.18),
            Height = Math.Max(120, Height * 0.24),
            RadiusX = 10,
            RadiusY = 10,
            Fill = Brushes.Black,
            Stroke = Brushes.White,
            StrokeThickness = 2,
            IsHitTestVisible = false
        };

        await Dispatcher.InvokeAsync(() =>
        {
            Canvas.SetTop(rectangle, Math.Max(0, Height * 0.38));
            AddElement(effectId, rectangle);

            DoubleAnimation animation = new()
            {
                From = -rectangle.Width,
                To = Width,
                Duration = new Duration(duration),
                RepeatBehavior = RepeatBehavior.Forever
            };

            rectangle.BeginAnimation(Canvas.LeftProperty, animation);
        });

        try
        {
            await Task.Delay(duration, cancellationToken);
        }
        finally
        {
            await Dispatcher.InvokeAsync(() => RemoveElement(effectId));
        }
    }

    public void ClearAllEffects()
    {
        RunOnUi(() =>
        {
            EffectCanvas.Children.Clear();
            _activeElements.Clear();
            _activeHudEffects.Clear();
            RefreshCurrentEffectHud();
        });
    }

    private void RefreshCurrentEffectHud()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(RefreshCurrentEffectHud);
            return;
        }

        DateTimeOffset now = DateTimeOffset.UtcNow;
        foreach (string expiredId in _activeHudEffects
                     .Where(pair => pair.Value.EndsAt <= now)
                     .Select(pair => pair.Key)
                     .ToList())
        {
            _activeHudEffects.Remove(expiredId);
        }

        ActiveHudEffect? current = _activeHudEffects.Values
            .OrderByDescending(effect => effect.StartedAt)
            .FirstOrDefault();

        if (current is null)
        {
            CurrentEffectNameTextBlock.Text = "No active effect";
            CurrentEffectTimerTextBlock.Text = _engineRunning
                ? "Waiting for the next effect"
                : "Chaos is stopped";
            return;
        }

        int additionalCount = Math.Max(0, _activeHudEffects.Count - 1);
        CurrentEffectNameTextBlock.Text = additionalCount > 0
            ? $"{current.Name}  (+{additionalCount})"
            : current.Name;

        TimeSpan remaining = current.EndsAt - now;
        CurrentEffectTimerTextBlock.Text = $"{FormatTime(remaining)} remaining";
    }

    private async Task AddForDurationAsync(
        string effectId,
        FrameworkElement element,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        await Dispatcher.InvokeAsync(() => AddElement(effectId, element));

        try
        {
            await Task.Delay(duration, cancellationToken);
        }
        finally
        {
            await Dispatcher.InvokeAsync(() => RemoveElement(effectId));
        }
    }

    private void AddElement(string effectId, FrameworkElement element)
    {
        RemoveElement(effectId);
        _activeElements[effectId] = element;
        EffectCanvas.Children.Add(element);
    }

    private void RemoveElement(string effectId)
    {
        if (_activeElements.Remove(effectId, out FrameworkElement? element))
        {
            element.BeginAnimation(OpacityProperty, null);
            EffectCanvas.Children.Remove(element);
        }
    }

    private static Task AnimateOpacityAsync(
        UIElement element,
        double to,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        DoubleAnimation animation = new()
        {
            To = to,
            Duration = new Duration(duration),
            FillBehavior = FillBehavior.HoldEnd
        };

        animation.Completed += (_, _) => completion.TrySetResult(true);
        element.BeginAnimation(OpacityProperty, animation);

        cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
        return completion.Task;
    }

    private void RunOnUi(Action action)
    {
        if (Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.Invoke(action);
        }
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero)
        {
            time = TimeSpan.Zero;
        }

        return time.TotalMinutes >= 1
            ? $"{(int)time.TotalMinutes:00}:{time.Seconds:00}"
            : $"00:{Math.Ceiling(time.TotalSeconds):00}";
    }

    private void MakeClickThrough()
    {
        WindowInteropHelper helper = new(this);
        int style = GetWindowLong(helper.Handle, GwlExStyle);
        SetWindowLong(
            helper.Handle,
            GwlExStyle,
            style | WsExTransparent | WsExToolWindow | WsExNoActivate);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr window, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr window, int index, int newStyle);

    private sealed record ActiveHudEffect(
        string Name,
        DateTimeOffset StartedAt,
        DateTimeOffset EndsAt);
}
