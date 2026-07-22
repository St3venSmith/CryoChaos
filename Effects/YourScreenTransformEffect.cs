using CryoChaos.Models;

namespace CryoChaos.Effects;

/// <summary>
/// Copy this class when adding a screen effect. To reuse an existing shader,
/// select any ScreenTransformMode below. To create a brand-new shader mode,
/// add the next explicit number to ScreenTransformMode, implement that number
/// in Shaders/ScreenEffects.hlsl, then remove ChaosEffectTemplate from the
/// copied and renamed class.
/// </summary>
[ChaosEffectTemplate]
public sealed class YourScreenTransformEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "your_screen_effect",
        Name = "Your Screen Effect",
        Description = "Describe the screen effect here.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 1,
        DurationSeconds = 8,
        CooldownSeconds = 90,
        CanStack = false
    };

    // Select the shader/transform used by this effect here. For example:
    // ScreenTransformMode.Grayscale
    // ScreenTransformMode.InvertColors
    // ScreenTransformMode.Pixelate
    // ScreenTransformMode.Kaleidoscope
    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.Kaleidoscope;
}
