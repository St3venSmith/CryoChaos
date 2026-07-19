using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Effects;

public abstract class KeybindChaosEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }
    protected abstract string[] ActionAliases { get; }

    protected virtual int RepeatCount(ChaosLevel level) => 1;

    protected virtual TimeSpan HoldDuration(ChaosLevel level) =>
        TimeSpan.FromMilliseconds(75);

    protected virtual TimeSpan RepeatDelay =>
        TimeSpan.FromMilliseconds(150);

    public async Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        if (context.RequireDestinyForeground &&
            !ForegroundWindowService.IsDestinyForeground())
        {
            return;
        }

        InputBinding? binding =
            context.Keybinds.ResolveActionForSimulation(ActionAliases);

        if (binding is null)
        {
            return;
        }

        int repeats = Math.Max(
            1,
            RepeatCount(context.SelectedLevel));

        for (int index = 0; index < repeats; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await context.Input.PressAsync(
                binding,
                HoldDuration(context.SelectedLevel),
                cancellationToken);

            if (index < repeats - 1)
            {
                await Task.Delay(RepeatDelay, cancellationToken);
            }
        }
    }
}

public sealed class SurpriseJumpEffect : KeybindChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "surprise_jump",
        Name = "Surprise Jump",
        Description = "Triggers the detected jump binding.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Low,
        Weight = 15,
        DurationSeconds = 1,
        CooldownSeconds = 35,
        CanStack = true
    };

    protected override string[] ActionAliases =>
    [
        "jump",
        "key_jump",
        "move_jump"
    ];

    protected override int RepeatCount(ChaosLevel level) =>
        level == ChaosLevel.Chaos ? 2 : 1;
}

public sealed class ForcedCrouchEffect : KeybindChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "forced_crouch",
        Name = "Duck",
        Description = "Triggers the detected crouch binding.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Low,
        Weight = 13,
        DurationSeconds = 1,
        CooldownSeconds = 40,
        CanStack = true
    };

    protected override string[] ActionAliases =>
    [
        "crouch",
        "toggle_crouch",
        "hold_crouch",
        "key_crouch"
    ];
}

public sealed class WeaponPanicEffect : KeybindChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "weapon_panic",
        Name = "Weapon Panic",
        Description = "Triggers the detected weapon-swap binding.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 10,
        DurationSeconds = 1,
        CooldownSeconds = 55,
        CanStack = true
    };

    protected override string[] ActionAliases =>
    [
        "switch_weapon",
        "switch_weapons",
        "weapon_swap",
        "swap_weapon",
        "swap_weapons",
        "next_weapon",
        "cycle_weapon",
        "cycle_weapons"
    ];

    protected override int RepeatCount(ChaosLevel level) =>
        level == ChaosLevel.Chaos ? 2 : 1;

    protected override TimeSpan HoldDuration(ChaosLevel level) =>
        TimeSpan.FromMilliseconds(100);
}

public sealed class StepBackwardEffect : KeybindChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "step_backward",
        Name = "Back Up",
        Description = "Briefly holds the detected move-backward binding.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 9,
        DurationSeconds = 1,
        CooldownSeconds = 65,
        CanStack = false
    };

    protected override string[] ActionAliases =>
    [
        "move_backward",
        "move_back",
        "backward",
        "key_move_backward"
    ];

    protected override TimeSpan HoldDuration(ChaosLevel level) =>
        level switch
        {
            ChaosLevel.Normal => TimeSpan.FromMilliseconds(250),
            ChaosLevel.Chaos => TimeSpan.FromMilliseconds(550),
            _ => TimeSpan.FromMilliseconds(150)
        };
}
