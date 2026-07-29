using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Views;

public partial class ScreenFilterLabWindow : Window
{
    private static readonly string ProfilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CryoChaos",
        "screen-filter.json");

    private NativeScreenEffectWindow? _overlay;
    private bool _ready;

    public ScreenFilterLabWindow()
    {
        InitializeComponent();

        ScreenTransformMode[] modes =
            Enum.GetValues<ScreenTransformMode>();
        PrimaryEffectComboBox.ItemsSource = modes;
        SecondaryEffectComboBox.ItemsSource = modes;
        PrimaryEffectComboBox.SelectedItem = ScreenTransformMode.None;
        SecondaryEffectComboBox.SelectedItem = ScreenTransformMode.None;

        LoadProfile();
        _ready = true;
        Closed += (_, _) => StopOverlay();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            IntPtr destinyWindow =
                DestinyWindowService.FindDestinyWindow();
            if (!DestinyWindowService.IsUsableWindow(destinyWindow))
            {
                MessageBox.Show(
                    this,
                    "Start Destiny 2 and make sure it is not minimized.",
                    "Destiny 2 not found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            StopOverlay();
            _overlay = new NativeScreenEffectWindow(
                destinyWindow,
                GetEffectModes());
            _overlay.SetFilterSettings(GetSettings());
            SaveProfile();
            StatusTextBlock.Text =
                "Running persistently — click-through and non-activating";

            // Only the native render surface belongs above Destiny. The lab
            // is a normal control window and must immediately yield keyboard
            // and mouse focus so the game can keep receiving input.
            Topmost = false;
            ForegroundWindowService.TryActivateDestinyWindow();
        }
        catch (Exception exception)
        {
            StopOverlay();
            StatusTextBlock.Text = "Could not start";
            MessageBox.Show(
                this,
                exception.Message,
                "Screen Filter Lab",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) =>
        StopOverlay();

    private void StopOverlay()
    {
        _overlay?.Dispose();
        _overlay = null;
        StatusTextBlock.Text = "Stopped";
    }

    private void LiveSetting_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        _overlay?.SetEffectModes(GetEffectModes());
        _overlay?.SetFilterSettings(GetSettings());
        SaveProfile();
    }

    private IReadOnlyList<ScreenTransformMode> GetEffectModes()
    {
        ScreenTransformMode primary =
            PrimaryEffectComboBox.SelectedItem is ScreenTransformMode first
                ? first
                : ScreenTransformMode.None;
        ScreenTransformMode secondary =
            SecondaryEffectComboBox.SelectedItem is ScreenTransformMode second
                ? second
                : ScreenTransformMode.None;

        ScreenTransformMode[] selected = [primary, secondary];
        ScreenTransformMode[] active = selected
            .Where(mode => mode != ScreenTransformMode.None)
            .Distinct()
            .ToArray();

        return active.Length == 0
            ? [ScreenTransformMode.None]
            : active;
    }

    private ScreenFilterSettings GetSettings() => new(
        (float)ExposureSlider.Value,
        (float)ContrastSlider.Value,
        (float)SaturationSlider.Value,
        (float)HueSlider.Value,
        (float)TemperatureSlider.Value,
        (float)TintSlider.Value,
        (float)GammaSlider.Value,
        (float)RedSlider.Value,
        (float)GreenSlider.Value,
        (float)BlueSlider.Value,
        (float)VignetteSlider.Value);

    private void ApplySettings(ScreenFilterSettings settings)
    {
        ExposureSlider.Value = settings.Exposure;
        ContrastSlider.Value = settings.Contrast;
        SaturationSlider.Value = settings.Saturation;
        HueSlider.Value = settings.HueDegrees;
        TemperatureSlider.Value = settings.Temperature;
        TintSlider.Value = settings.Tint;
        GammaSlider.Value = settings.Gamma;
        RedSlider.Value = settings.Red;
        GreenSlider.Value = settings.Green;
        BlueSlider.Value = settings.Blue;
        VignetteSlider.Value = settings.Vignette;
    }

    private void PresetButton_Click(object sender, RoutedEventArgs e)
    {
        string preset = (sender as Button)?.Tag as string ?? "Neutral";
        ScreenFilterSettings settings = preset switch
        {
            "Vibrant" => new(0.08f, 1.12f, 1.45f, 0, 0.05f, 0, 1, 1.04f, 1, 1.03f, 0.12f),
            "Cinematic" => new(-0.08f, 1.18f, 0.86f, -5, -0.08f, 0.03f, 0.92f, 1.02f, 0.98f, 1.08f, 0.35f),
            "Cold" => new(0, 1.08f, 0.92f, -8, -0.55f, 0.05f, 1, 0.9f, 1.02f, 1.18f, 0.16f),
            "Warm" => new(0.05f, 1.05f, 1.08f, 5, 0.55f, 0, 1.02f, 1.16f, 1.02f, 0.9f, 0.14f),
            "Noir" => new(-0.05f, 1.35f, 0, 0, 0, 0, 0.92f, 1, 1, 1, 0.42f),
            _ => ScreenFilterSettings.Default
        };

        ApplySettings(settings);
        LiveSetting_Changed(sender, e);
    }

    private void SaveProfile()
    {
        try
        {
            Directory.CreateDirectory(
                Path.GetDirectoryName(ProfilePath)!);
            ScreenFilterProfile profile = new(
                PrimaryEffectComboBox.SelectedItem is ScreenTransformMode primary
                    ? primary
                    : ScreenTransformMode.None,
                SecondaryEffectComboBox.SelectedItem is ScreenTransformMode secondary
                    ? secondary
                    : ScreenTransformMode.None,
                GetSettings());
            File.WriteAllText(
                ProfilePath,
                JsonSerializer.Serialize(
                    profile,
                    new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception exception)
        {
            CrashLogService.WriteException(
                "SCREEN FILTER PROFILE SAVE",
                exception);
        }
    }

    private void LoadProfile()
    {
        try
        {
            if (!File.Exists(ProfilePath))
            {
                ApplySettings(ScreenFilterSettings.Default);
                return;
            }

            ScreenFilterProfile? profile =
                JsonSerializer.Deserialize<ScreenFilterProfile>(
                    File.ReadAllText(ProfilePath));
            if (profile is null)
            {
                return;
            }

            PrimaryEffectComboBox.SelectedItem = profile.Primary;
            SecondaryEffectComboBox.SelectedItem = profile.Secondary;
            ApplySettings(profile.Settings);
        }
        catch (Exception exception)
        {
            ApplySettings(ScreenFilterSettings.Default);
            CrashLogService.WriteException(
                "SCREEN FILTER PROFILE LOAD",
                exception);
        }
    }

    private sealed record ScreenFilterProfile(
        ScreenTransformMode Primary,
        ScreenTransformMode Secondary,
        ScreenFilterSettings Settings);
}
