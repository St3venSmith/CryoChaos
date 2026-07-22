using CryoChaos.Models;

namespace CryoChaos.Effects;

/// <summary>
/// Copy this template to create an effect that holds several detected Destiny
/// inputs simultaneously. Rename it and remove ChaosEffectTemplate.
/// </summary>
[ChaosEffectTemplate]
public sealed class YourMultiInputEffect : MultiInputChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "your_multi_input_effect",
        Name = "Your Multi-Input Effect",
        Description = "Holds forward and right at the same time.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 5,
        DurationSeconds = 2,
        CooldownSeconds = 60,
        CanStack = false
    };

    // One inner array per input. Each inner array can contain alternate cvars
    // action names; the first detected binding is used.
    protected override string[][] ActionAliasGroups =>
    [
        ["move_forward", "forward", "key_move_forward"],
        ["move_right", "strafe_right", "right", "key_move_right"]
    ];

    protected override TimeSpan HoldDuration(ChaosLevel level) =>
        TimeSpan.FromMilliseconds(750);
}
