using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Effects;

public abstract class ScreenTransformEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }

    protected abstract ScreenTransformMode TransformMode { get; }

    protected virtual TimeSpan GetDuration(ChaosLevel level)
    {
        return TimeSpan.FromSeconds(Definition.DurationSeconds);
    }

    public Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        // The foreground safety option applies only to synthetic key input.
        // Screen effects must still create their capture window when the
        // CryoChaos control panel or a diagnostic preview has focus. The old
        // check made the HUD announce an effect while silently skipping it.
        return context.ScreenTransform.ShowAsync(
            TransformMode,
            context.ScaleEffectDuration(GetDuration(context.SelectedLevel)),
            cancellationToken);
    }
}

public sealed class UpsideDownEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "upside_down",
        Name = "Upside Down",
        Description = "Rotates the live game image by 180 degrees.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 1,
        DurationSeconds = 8,
        CooldownSeconds = 120,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.Rotate180;
}

public sealed class SidewaysLeftEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "sideways_left",
        Name = "Sideways Left",
        Description = "Rotates the live game image 90 degrees counterclockwise.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 1,
        DurationSeconds = 8,
        CooldownSeconds = 150,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.Rotate90CounterClockwise;
}

public sealed class SidewaysRightEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "sideways_right",
        Name = "Sideways Right",
        Description = "Rotates the live game image 90 degrees clockwise.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 1,
        DurationSeconds = 8,
        CooldownSeconds = 150,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.Rotate90Clockwise;
}

public sealed class MirrorScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "mirror_screen",
        Name = "Mirror Mode",
        Description = "Flips the live game image from left to right.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 1,
        DurationSeconds = 9,
        CooldownSeconds = 110,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.FlipHorizontal;
}

public sealed class VerticalFlipEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "vertical_flip",
        Name = "Vertical Flip",
        Description = "Flips the live game image from top to bottom.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 1,
        DurationSeconds = 8,
        CooldownSeconds = 140,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.FlipVertical;
}

public sealed class GrayscaleScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "grayscale_screen",
        Name = "Old Movie",
        Description = "Drains all color from the live game image.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 1,
        DurationSeconds = 10,
        CooldownSeconds = 90,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.Grayscale;
}

public sealed class InvertColorsEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "invert_colors",
        Name = "Negative World",
        Description = "Inverts every color in the live game image.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 1,
        DurationSeconds = 7,
        CooldownSeconds = 120,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.InvertColors;
}

public sealed class PixelateScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "pixelate_screen",
        Name = "Low Resolution",
        Description = "Turns the live game image into large animated pixels.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 1,
        DurationSeconds = 9,
        CooldownSeconds = 100,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.Pixelate;
}

public sealed class ChromaticAberrationEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "chromatic_aberration",
        Name = "RGB Split",
        Description = "Separates red, green, and blue channels around the screen.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 1,
        DurationSeconds = 9,
        CooldownSeconds = 110,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.ChromaticAberration;
}

public sealed class WaveScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "wave_screen",
        Name = "Reality Wave",
        Description = "Continuously bends the live game image in two directions.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 1,
        DurationSeconds = 8,
        CooldownSeconds = 130,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.Wave;
}

public sealed class KaleidoscopeScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "kaleidoscope_screen",
        Name = "Kaleidoscope",
        Description = "Folds the live game image into rotating mirrored wedges.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 1,
        DurationSeconds = 7,
        CooldownSeconds = 180,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.Kaleidoscope;
}

public sealed class SepiaScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "sepia_screen",
        Name = "Ancient Film",
        Description = "Recolors the live game image with a warm sepia palette.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Low,
        Weight = 1,
        DurationSeconds = 9,
        CooldownSeconds = 85,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode => ScreenTransformMode.Sepia;
}

public sealed class PosterizeScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "posterize_screen",
        Name = "Comic Colors",
        Description = "Reduces the live image to a handful of bold color levels.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 1,
        DurationSeconds = 9,
        CooldownSeconds = 95,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode => ScreenTransformMode.Posterize;
}

public sealed class ScanlinesScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "scanlines_screen",
        Name = "CRT Mode",
        Description = "Adds animated dark scanlines and display flicker.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 1,
        DurationSeconds = 8,
        CooldownSeconds = 100,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode => ScreenTransformMode.Scanlines;
}

public sealed class VignettePulseScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "vignette_pulse_screen",
        Name = "Closing In",
        Description = "Pulses a deep GPU-rendered vignette around the game image.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 1,
        DurationSeconds = 9,
        CooldownSeconds = 105,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode => ScreenTransformMode.VignettePulse;
}

public sealed class ZoomPulseScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "zoom_pulse_screen",
        Name = "Breathing Zoom",
        Description = "Continuously zooms the live image in and out.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 1,
        DurationSeconds = 8,
        CooldownSeconds = 120,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode => ScreenTransformMode.ZoomPulse;
}

public sealed class DigitalGlitchScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "digital_glitch_screen",
        Name = "Signal Lost",
        Description = "Shifts random horizontal slices and splits color channels.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 1,
        DurationSeconds = 7,
        CooldownSeconds = 135,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode => ScreenTransformMode.DigitalGlitch;
}

public sealed class LensWarpScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "lens_warp_screen",
        Name = "Fish Eye",
        Description = "Bends the live image through a strong curved lens.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 1,
        DurationSeconds = 9,
        CooldownSeconds = 110,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode => ScreenTransformMode.LensWarp;
}

public sealed class HeatVisionScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "heat_vision_screen",
        Name = "Heat Vision",
        Description = "Maps brightness into a blue, red, and yellow thermal palette.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 1,
        DurationSeconds = 9,
        CooldownSeconds = 105,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode => ScreenTransformMode.HeatVision;
}

public sealed class ColorCycleScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "color_cycle_screen",
        Name = "Chromatic Drift",
        Description = "Continuously rotates every color through the spectrum.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 1,
        DurationSeconds = 10,
        CooldownSeconds = 100,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode => ScreenTransformMode.ColorCycle;
}

public sealed class ScreenShakeEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "screen_shake",
        Name = "Earthquake",
        Description = "Jolts the captured game image in rapid random bursts.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 1,
        DurationSeconds = 7,
        CooldownSeconds = 125,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode => ScreenTransformMode.ScreenShake;
}

public sealed class MirrorTilesScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "mirror_tiles_screen",
        Name = "Mirror Maze",
        Description = "Repeats the live view as a grid of mirrored tiles.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 1,
        DurationSeconds = 8,
        CooldownSeconds = 140,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode => ScreenTransformMode.MirrorTiles;
}

public sealed class DreamBlurScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "dream_blur_screen",
        Name = "Dream State",
        Description = "Softens and pulses the game image with a surreal color cast.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 1,
        DurationSeconds = 8,
        CooldownSeconds = 135,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode => ScreenTransformMode.DreamBlur;
}

public sealed class NightVisionScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "night_vision_screen",
        Name = "Night Vision",
        Description = "Turns the live view into a noisy green night-vision feed.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Low,
        Weight = 1,
        DurationSeconds = 9,
        CooldownSeconds = 90,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode => ScreenTransformMode.NightVision;
}
