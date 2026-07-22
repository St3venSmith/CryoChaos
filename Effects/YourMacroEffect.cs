using CryoChaos.Models;

namespace CryoChaos.Effects;

/// <summary>
/// Copy this template, rename it, change the ordered Steps, and remove
/// ChaosEffectTemplate from the finished effect.
/// </summary>
[ChaosEffectTemplate]
public sealed class YourMacroEffect : MacroChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "your_macro_effect",
        Name = "Your Macro Effect",
        Description = "Runs several inputs in order.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 4,
        DurationSeconds = 3,
        CooldownSeconds = 90,
        CanStack = false
    };

    protected override IReadOnlyList<InputMacroStep> Steps { get; } =
    [
        new MacroMouseMove(0, 1200, 120),
        new MacroDelay(100),
        new MacroPressAction(
            ["weapon_3", "select_heavy_weapon"],
            HoldMilliseconds: 100,
            FallbackVirtualKey: 0x33),
        new MacroDelay(250),
        new MacroPressMouseButton(MouseInputButton.Left, 120)
    ];
}
