using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace CryoChaos.Views;

public partial class ChaosAdWindow : Window
{
    private readonly TimeSpan _skipDelay;
    private readonly TaskCompletionSource _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly DispatcherTimer _timer;
    private DateTimeOffset _startedAt;
    private CancellationToken _cancellationToken;
    private CancellationTokenRegistration _cancellationRegistration;
    private bool _allowClose;

    private ChaosAdWindow(TimeSpan skipDelay)
    {
        InitializeComponent();
        _skipDelay = skipDelay;
        _timer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(16),
            DispatcherPriority.Render,
            Timer_Tick,
            Dispatcher);
        Closing += Window_Closing;
    }

    public static async Task ShowAsync(
        TimeSpan skipDelay,
        CancellationToken cancellationToken)
    {
        ChaosAdWindow? window = null;

        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            window = new ChaosAdWindow(skipDelay)
            {
                Owner = Application.Current.MainWindow
            };

            window.Begin(cancellationToken);
            window.Show();
            window.Activate();
        });

        await window!._completion.Task;
    }

    private void Begin(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _startedAt = DateTimeOffset.UtcNow;
        _cancellationRegistration = cancellationToken.Register(() =>
            Dispatcher.InvokeAsync(CancelAndClose));
        UpdateCountdown();
        _timer.Start();
    }

    private void Timer_Tick(object? sender, EventArgs e) =>
        UpdateCountdown();

    private void UpdateCountdown()
    {
        TimeSpan elapsed = DateTimeOffset.UtcNow - _startedAt;
        TimeSpan remaining = _skipDelay - elapsed;
        double progress = _skipDelay.TotalMilliseconds <= 0
            ? 1
            : Math.Clamp(
                elapsed.TotalMilliseconds / _skipDelay.TotalMilliseconds,
                0,
                1);

        CountdownProgressBar.Value = progress;

        if (remaining > TimeSpan.Zero)
        {
            double roundedSeconds =
                Math.Ceiling(remaining.TotalMilliseconds / 100) / 10.0;
            string seconds = roundedSeconds.ToString("0.0");
            CountdownTextBlock.Text =
                $"Movement locked for {seconds} seconds";
            SkipButton.Content = $"Skip in {seconds}";
            return;
        }

        _timer.Stop();
        CountdownTextBlock.Text = "Ad finished — press Skip to resume movement";
        SkipButton.Content = "Skip ad";
        SkipButton.IsEnabled = true;
        SkipButton.Focus();
    }

    private void SkipButton_Click(object sender, RoutedEventArgs e)
    {
        if (!SkipButton.IsEnabled)
        {
            return;
        }

        _allowClose = true;
        _timer.Stop();
        _cancellationRegistration.Dispose();
        _completion.TrySetResult();
        Close();
    }

    private void CancelAndClose()
    {
        if (_allowClose)
        {
            return;
        }

        _allowClose = true;
        _timer.Stop();
        _cancellationRegistration.Dispose();
        _completion.TrySetCanceled(_cancellationToken);
        Close();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
        }
    }
}
