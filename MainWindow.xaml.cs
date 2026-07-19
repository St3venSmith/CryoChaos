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
        _screenTransform = new ScreenTransformService(_overlay);
        _engine = new ChaosEngine(
            _overlay,
            _keybinds,
            _input,
            _screenTransform);

        Effects = _engine.Effects;
        DetectedBindings = _keybinds.DisplayBindings;
        DataContext = this;

        ModeComboBox.ItemsSource = Enum.GetValues<ChaosLevel>();
        ModeComboBox.SelectedItem = _settings.SelectedLevel;
        MinimumIntervalTextBox.Text = _settings.MinimumIntervalSeconds.ToString();
        MaximumIntervalTextBox.Text = _settings.MaximumIntervalSeconds.ToString();
        RequireForegroundCheckBox.IsChecked = _settings.RequireDestinyForeground;

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

    private async void TriggerNowButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
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
        _keybinds.Dispose();
        _overlay.Close();
    }
}
