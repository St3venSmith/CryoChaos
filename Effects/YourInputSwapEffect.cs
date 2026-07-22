using CryoChaos.Models;

namespace CryoChaos.Effects;

/// <summary>
/// Copy this template to swap any two detected keyboard actions temporarily.
/// Rename it and remove ChaosEffectTemplate from the finished effect.
/// </summary>
[ChaosEffectTemplate]
public sealed class YourInputSwapEffect : InputSwapChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "your_input_swap_effect",
        Name = "Your Input Swap",
        Description = "Swaps left and right movement temporarily.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 5,
        DurationSeconds = 8,
        CooldownSeconds = 120,
        CanStack = false
    };

    protected override string[] FirstActionAliases =>
        ["move_left", "strafe_left", "left", "key_move_left"];

    protected override string[] SecondActionAliases =>
        ["move_right", "strafe_right", "right", "key_move_right"];
}
