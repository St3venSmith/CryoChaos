using CryoChaos.Models;

namespace CryoChaos.Effects;

/// <summary>
/// Copy this template to temporarily disable any detected keyboard key or
/// mouse button. Rename it and remove ChaosEffectTemplate.
/// </summary>
[ChaosEffectTemplate]
public sealed class YourInputDisableEffect : InputDisableChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "your_input_disable_effect",
        Name = "Your Disabled Input",
        Description = "Temporarily disables the selected action.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 5,
        DurationSeconds = 6,
        CooldownSeconds = 90,
        CanStack = false
    };

    protected override string[] ActionAliases =>
        ["fire", "attack", "primary_fire", "shoot", "key_fire"];
}
