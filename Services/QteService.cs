using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace CryoChaos.Services;

public sealed record QteOptions(
    IReadOnlyList<ushort> AllowedKeys,
    int PromptCount,
    TimeSpan TimePerPrompt,
    string Title);

public sealed class QteService : IDisposable
{
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x20;
    private const int WsExToolwindow = 0x80;
    private const int WsExNoactivate = 0x08000000;
    private readonly Dispatcher _dispatcher;
    private Window? _window;
    private TextBlock? _prompt;
    private TextBlock? _status;

    public QteService(Dispatcher dispatcher) => _dispatcher = dispatcher;

    public async Task<bool> RunAsync(
        QteOptions options,
        Random random,
        CancellationToken cancellationToken)
    {
        if (options.AllowedKeys.Count == 0 || options.PromptCount < 1)
        {
            throw new ArgumentException("A QTE needs at least one key and one prompt.");
        }

        Show(options.Title);
        try
        {
            for (int step = 0; step < options.PromptCount; step++)
            {
                ushort expected = options.AllowedKeys[random.Next(options.AllowedKeys.Count)];
                SetPrompt(expected, step + 1, options.PromptCount);
                await WaitForAllReleasedAsync(options.AllowedKeys, cancellationToken);
                HashSet<ushort> downKeys = [];

                DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(options.TimePerPrompt);
                while (DateTimeOffset.UtcNow < deadline)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (ushort key in options.AllowedKeys)
                    {
                        bool isDown = IsKeyDown(key);
                        bool pressedNow = isDown && !downKeys.Contains(key);
                        if (isDown)
                        {
                            downKeys.Add(key);
                        }
                        else
                        {
                            downKeys.Remove(key);
                        }

                        if (!pressedNow)
                        {
                            continue;
                        }

                        if (key != expected)
                        {
                            SetStatus("Wrong key!");
                            return false;
                        }

                        SetStatus("Good!");
                        goto NextPrompt;
                    }

                    await Task.Delay(8, cancellationToken);
                }

                SetStatus("Too slow!");
                return false;

            NextPrompt:
                await Task.Delay(100, cancellationToken);
            }

            SetStatus("QTE complete!");
            await Task.Delay(350, cancellationToken);
            return true;
        }
        finally
        {
            Hide();
        }
    }

    public async Task<bool> RunMathAsync(
        string question,
        int expectedAnswer,
        TimeSpan timeLimit,
        Action wrongAnswer,
        CancellationToken cancellationToken)
    {
        Show("Type the answer, then press ENTER");
        string answer = string.Empty;
        SetTextPrompt(question, "Answer: _");
        ushort[] inputKeys = Enumerable.Range(0x30, 10)
            .Concat(Enumerable.Range(0x60, 10))
            .Select(value => (ushort)value)
            .Append((ushort)0x08)
            .Append((ushort)0x0D)
            .ToArray();
        await WaitForAllReleasedAsync(inputKeys, cancellationToken);
        HashSet<ushort> downKeys = [];

        try
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(timeLimit);
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (ushort key in inputKeys)
                {
                    bool isDown = IsKeyDown(key);
                    bool pressedNow = isDown && !downKeys.Contains(key);
                    if (isDown)
                    {
                        downKeys.Add(key);
                    }
                    else
                    {
                        downKeys.Remove(key);
                    }

                    if (!pressedNow)
                    {
                        continue;
                    }

                    if (key is >= 0x30 and <= 0x39 && answer.Length < 5)
                    {
                        answer += (char)key;
                        SetStatus($"Answer: {answer}");
                    }
                    else if (key is >= 0x60 and <= 0x69 && answer.Length < 5)
                    {
                        answer += (char)('0' + key - 0x60);
                        SetStatus($"Answer: {answer}");
                    }
                    else if (key == 0x08 && answer.Length > 0)
                    {
                        answer = answer[..^1];
                        SetStatus($"Answer: {(answer.Length == 0 ? "_" : answer)}");
                    }
                    else if (key == 0x0D)
                    {
                        bool correct = int.TryParse(answer, out int submitted) &&
                            submitted == expectedAnswer;
                        if (correct)
                        {
                            SetStatus("Correct!");
                            await Task.Delay(400, cancellationToken);
                            return true;
                        }

                        answer = string.Empty;
                        wrongAnswer();
                        SetStatus("Wrong — try again. Answer: _");
                    }
                }

                await Task.Delay(8, cancellationToken);
            }

            SetStatus("Time expired!");
            return false;
        }
        finally
        {
            Hide();
        }
    }

    public async Task ShowJumpScareAsync(
        string scareText,
        TimeSpan duration,
        CancellationToken cancellationToken)
    {
        _dispatcher.Invoke(() =>
        {
            TextBlock face = new()
            {
                Text = scareText,
                FontSize = 330,
                FontWeight = FontWeights.Black,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid background = new()
            {
                Background = new SolidColorBrush(Color.FromArgb(248, 125, 0, 0))
            };
            background.Children.Add(face);
            _window = new Window
            {
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = true,
                ShowActivated = false,
                Content = background
            };
            _window.SourceInitialized += (_, _) =>
            {
                IntPtr hwnd = new WindowInteropHelper(_window).Handle;
                int style = GetWindowLong(hwnd, GwlExstyle);
                SetWindowLong(hwnd, GwlExstyle,
                    style | WsExTransparent | WsExToolwindow | WsExNoactivate);
                GameMonitorPlacementService.FillGameMonitor(_window, activate: false);
            };
            _window.Show();
        });

        try
        {
            await Task.Delay(duration, cancellationToken);
        }
        finally
        {
            Hide();
        }
    }

    private void Show(string title) => _dispatcher.Invoke(() =>
    {
        _prompt = new TextBlock
        {
            FontSize = 54,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _status = new TextBlock
        {
            Text = title,
            FontSize = 17,
            Foreground = new SolidColorBrush(Color.FromRgb(190, 178, 255)),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        StackPanel panel = new();
        panel.Children.Add(_prompt);
        panel.Children.Add(_status);
        Border card = new()
        {
            Background = new SolidColorBrush(Color.FromArgb(235, 15, 18, 29)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(121, 92, 255)),
            BorderThickness = new Thickness(3),
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(36, 22, 36, 22),
            Child = panel
        };
        _window = new Window
        {
            Width = 430,
            Height = 190,
            AllowsTransparency = true,
            Background = Brushes.Transparent,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost = true,
            ShowActivated = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Content = card
        };
        _window.SourceInitialized += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(_window).Handle;
            int style = GetWindowLong(hwnd, GwlExstyle);
            SetWindowLong(hwnd, GwlExstyle, style | WsExTransparent | WsExToolwindow | WsExNoactivate);
            GameMonitorPlacementService.CenterOnGameMonitor(_window, activate: false);
        };
        _window.Show();
    });

    private void SetPrompt(ushort key, int current, int total) =>
        _dispatcher.Invoke(() =>
        {
            if (_prompt is not null)
            {
                _prompt.Text = $"{KeyName(key)}   {current}/{total}";
            }
            if (_status is not null)
            {
                _status.Text = "Press the shown key";
            }
        });

    private void SetTextPrompt(string prompt, string status) =>
        _dispatcher.Invoke(() =>
        {
            if (_prompt is not null)
            {
                _prompt.Text = prompt;
                _prompt.FontSize = 38;
            }
            if (_status is not null)
            {
                _status.Text = status;
            }
        });

    private void SetStatus(string text) => _dispatcher.Invoke(() =>
    {
        if (_status is not null)
        {
            _status.Text = text;
        }
    });

    private void Hide() => _dispatcher.Invoke(() =>
    {
        _window?.Close();
        _window = null;
        _prompt = null;
        _status = null;
    });

    private static async Task WaitForAllReleasedAsync(
        IEnumerable<ushort> keys,
        CancellationToken cancellationToken)
    {
        ushort[] watchedKeys = keys.Distinct().ToArray();
        while (watchedKeys.Any(IsKeyDown))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(8, cancellationToken);
        }
    }

    private static bool IsKeyDown(ushort key) =>
        (GetAsyncKeyState(key) & 0x8000) != 0;

    private static string KeyName(ushort key) => key switch
    {
        0x20 => "SPACE",
        0x25 => "LEFT",
        0x26 => "UP",
        0x27 => "RIGHT",
        0x28 => "DOWN",
        >= 0x70 and <= 0x7B => $"F{key - 0x6F}",
        >= 0x30 and <= 0x5A => ((char)key).ToString(),
        _ => $"VK {key:X2}"
    };

    public void Dispose() => Hide();

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
