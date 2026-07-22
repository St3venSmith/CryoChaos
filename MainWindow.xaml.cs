using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CryoChaos.Models;
using CryoChaos.Services;
using CryoChaos.Views;

namespace CryoChaos;

public partial class MainWindow : Window
{
    private readonly OverlayWindow _overlay;
    private readonly DestinyKeybindService _keybinds;
    private readonly KeyboardInputService _input;
    private readonly KeyboardRemapService _inputRemapper;
    private readonly RawMouseEffectService _rawMouseEffects;
    private readonly SoundEffectService _soundEffects;
    private readonly GameAudioEffectService _gameAudioEffects;
    private readonly VideoOverlayService _videoOverlay;
    private readonly QteService _qte;
    private readonly ScreenTransformService _screenTransform;
    private readonly ChaosEngine _engine;
    private readonly SettingsService _settingsService;
    private AppSettings _settings;

    public MainWindow()
    {
        InitializeComponent();

        _settingsService = new SettingsService();
        _settings = _settingsService.Load();

        _overlay = new OverlayWindow();
        _keybinds = new DestinyKeybindService();
        _input = new KeyboardInputService();
        _inputRemapper = new KeyboardRemapService(Dispatcher);
        _rawMouseEffects = new RawMouseEffectService(this);
        _soundEffects = new SoundEffectService();
        _gameAudioEffects = new GameAudioEffectService();
        _videoOverlay = new VideoOverlayService(Dispatcher);
        _qte = new QteService(Dispatcher);
        _screenTransform = new ScreenTransformService(_overlay);
        _engine = new ChaosEngine(
            _overlay,
            _keybinds,
            _input,
            _inputRemapper,
            _rawMouseEffects,
            _soundEffects,
            _gameAudioEffects,
            _videoOverlay,
            _qte,
            _screenTransform);

        Effects = _engine.Effects;
        DetectedBindings = _keybinds.DisplayBindings;
        DataContext = this;

        ModeComboBox.ItemsSource = Enum.GetValues<ChaosLevel>();
        ModeComboBox.SelectedItem = _settings.SelectedLevel;
        MinimumIntervalTextBox.Text = _settings.MinimumIntervalSeconds.ToString();
        MaximumIntervalTextBox.Text = _settings.MaximumIntervalSeconds.ToString();
        RequireForegroundCheckBox.IsChecked = _settings.RequireDestinyForeground;
        int maximumActiveEffects = Math.Clamp(
            _settings.MaximumActiveEffects,
            1,
            ChaosEngine.MaximumSupportedActiveEffects);
        MaximumEffectsTextBox.Text = maximumActiveEffects.ToString();
        _engine.SetMaximumActiveEffects(maximumActiveEffects);

        foreach (ChaosEffectDefinition effect in Effects)
        {
            effect.Enabled = !_settings.DisabledEffectIds.Contains(effect.Id);
        }

        _engine.StatusChanged += (_, status) => Dispatcher.Invoke(() => StatusTextBlock.Text = status);
        _engine.EffectStarted += (_, effect) => AddLog($"Started: {effect}");
        _engine.EffectFinished += (_, effect) => AddLog($"Finished: {effect}");

        _keybinds.BindingsChanged += (_, _) => AddLog($"Loaded {DetectedBindings.Count} Destiny bindings.");
        _keybinds.LoadFailed += (_, message) => AddLog($"Keybind load failed: {message}");

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    public ObservableCollection<ChaosEffectDefinition> Effects { get; }
    public ObservableCollection<DestinyBinding> DetectedBindings { get; }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _overlay.Show();
        _keybinds.StartWatching();
        AddLog("CryoChaos ready.");
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            ChaosLevel level = GetSelectedLevel();
            int minimum = ParsePositiveInteger(MinimumIntervalTextBox.Text, "Minimum interval");
            int maximum = ParsePositiveInteger(MaximumIntervalTextBox.Text, "Maximum interval");
            _engine.SetMaximumActiveEffects(ParseMaximumEffects());

            _engine.Start(
                level,
                minimum,
                maximum,
                RequireForegroundCheckBox.IsChecked == true);

            SaveSettings();
            AddLog($"Engine started in {level} mode. Eligible effects include all tiers at or below {level}.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not start", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        _engine.Stop();
        SaveSettings();
        AddLog("Engine stopped.");
    }

    private void EnableAllEffectsButton_Click(object sender, RoutedEventArgs e) =>
        SetAllEffectsEnabled(true);

    private void DisableAllEffectsButton_Click(object sender, RoutedEventArgs e) =>
        SetAllEffectsEnabled(false);

    private void SetAllEffectsEnabled(bool enabled)
    {
        foreach (ChaosEffectDefinition effect in Effects)
        {
            effect.Enabled = enabled;
        }

        EffectsGrid.Items.Refresh();
        SaveSettings();
        AddLog(enabled ? "All effects enabled." : "All effects disabled.");
    }

    private async void TriggerNowButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _engine.SetMaximumActiveEffects(ParseMaximumEffects());
            await _engine.TriggerRandomNowAsync(
                GetSelectedLevel(),
                RequireForegroundCheckBox.IsChecked == true);
        }
        catch (Exception ex)
        {
            AddLog($"Manual trigger failed: {ex.Message}");
        }
    }

    private void RefreshBindingsButton_Click(object sender, RoutedEventArgs e)
    {
        _keybinds.Reload();
    }

    private void OpenBindingsFolderButton_Click(object sender, RoutedEventArgs e)
    {
        string? folder = Path.GetDirectoryName(DestinyKeybindService.DefaultCvarsPath);
        if (folder is null || !Directory.Exists(folder))
        {
            MessageBox.Show(
                this,
                $"The Destiny preferences folder was not found.\n\nExpected path:\n{folder}",
                "Folder not found",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true
        });
    }

    private void TestGameCaptureButton_Click(object sender, RoutedEventArgs e) =>
        OpenCaptureDiagnostic(captureMonitor: false);

    private void TestMonitorCaptureButton_Click(object sender, RoutedEventArgs e) =>
        OpenCaptureDiagnostic(captureMonitor: true);

    private async void TestMouseMoveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            MouseMovementDiagnostic result =
                await _input.TestMouseMovementAsync();

            string message = result.WindowsAcceptedMovement
                ? $"Windows mouse test passed. Requested ({result.RequestedX}, {result.RequestedY}); observed ({result.ObservedX}, {result.ObservedY})."
                : "Windows mouse test failed: SendInput returned success, but the cursor position did not change.";

            AddLog(message);
            MessageBox.Show(
                this,
                message,
                "Mouse movement diagnostic",
                MessageBoxButton.OK,
                result.WindowsAcceptedMovement
                    ? MessageBoxImage.Information
                    : MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            AddLog($"Mouse movement diagnostic failed: {ex.Message}");
            MessageBox.Show(
                this,
                ex.Message,
                "Mouse movement diagnostic failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void OpenCaptureDiagnostic(bool captureMonitor)
    {
        IntPtr destinyWindow = DestinyWindowService.FindDestinyWindow();
        if (!DestinyWindowService.IsUsableWindow(destinyWindow))
        {
            MessageBox.Show(
                this,
                "Start Destiny 2 and make sure it is not minimized, then run the capture test again.",
                "Destiny 2 not found",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        CaptureDiagnosticWindow diagnostic = new(
            destinyWindow,
            captureMonitor)
        {
            Owner = this
        };

        diagnostic.Show();
        AddLog(captureMonitor
            ? "Opened monitor-capture diagnostic."
            : "Opened Destiny-window capture diagnostic.");
    }

    private ChaosLevel GetSelectedLevel() =>
        ModeComboBox.SelectedItem is ChaosLevel level ? level : ChaosLevel.Normal;

    private static int ParsePositiveInteger(string text, string fieldName)
    {
        if (!int.TryParse(text, out int value) || value < 1)
        {
            throw new InvalidOperationException($"{fieldName} must be a whole number greater than zero.");
        }

        return value;
    }

    private int ParseMaximumEffects()
    {
        int value = ParsePositiveInteger(MaximumEffectsTextBox.Text, "Maximum effects");
        if (value > ChaosEngine.MaximumSupportedActiveEffects)
        {
            throw new InvalidOperationException(
                $"Maximum effects must be between 1 and {ChaosEngine.MaximumSupportedActiveEffects}.");
        }

        return value;
    }

    private void AddLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            EventLogListBox.Items.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
            while (EventLogListBox.Items.Count > 200)
            {
                EventLogListBox.Items.RemoveAt(EventLogListBox.Items.Count - 1);
            }
        });
    }

    private void SaveSettings()
    {
        _settings = new AppSettings
        {
            SelectedLevel = GetSelectedLevel(),
            MinimumIntervalSeconds = int.TryParse(MinimumIntervalTextBox.Text, out int minimum) ? minimum : 35,
            MaximumIntervalSeconds = int.TryParse(MaximumIntervalTextBox.Text, out int maximum) ? maximum : 70,
            RequireDestinyForeground = RequireForegroundCheckBox.IsChecked == true,
            MaximumActiveEffects = int.TryParse(MaximumEffectsTextBox.Text, out int maxEffects)
                ? Math.Clamp(maxEffects, 1, ChaosEngine.MaximumSupportedActiveEffects)
                : 3,
            DisabledEffectIds = Effects
                .Where(effect => !effect.Enabled)
                .Select(effect => effect.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
        };

        try
        {
            _settingsService.Save(_settings);
        }
        catch (Exception ex)
        {
            AddLog($"Settings save failed: {ex.Message}");
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveSettings();
        _engine.Dispose();
        _screenTransform.Dispose();
        _rawMouseEffects.Dispose();
        _qte.Dispose();
        _videoOverlay.Dispose();
        _soundEffects.Dispose();
        _gameAudioEffects.Dispose();
        _inputRemapper.Dispose();
        _keybinds.Dispose();
        _overlay.Close();
    }
}
