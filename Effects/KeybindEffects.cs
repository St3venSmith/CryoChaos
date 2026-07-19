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
                await Task.Delay(
                    RepeatDelay,
                    cancellationToken);
            }
        }
    }
}

/// <summary>
/// Base class for chaos effects that move the mouse/camera through SendInput.
/// These effects are still classified as Keybind effects in the UI.
/// </summary>
public abstract class MouseMoveChaosEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }

    /// <summary>
    /// Returns the total relative movement for one pass.
    /// Positive X = right, negative X = left.
    /// Positive Y = down, negative Y = up.
    /// </summary>
    protected abstract (int DeltaX, int DeltaY) GetMovement(
        ChaosLevel level,
        Random random);

    protected virtual TimeSpan MovementDuration(
        ChaosLevel level)
    {
        return level switch
        {
            ChaosLevel.Low =>
                TimeSpan.FromMilliseconds(90),

            ChaosLevel.Normal =>
                TimeSpan.FromMilliseconds(140),

            ChaosLevel.Chaos =>
                TimeSpan.FromMilliseconds(220),

            _ =>
                TimeSpan.FromMilliseconds(120)
        };
    }

    protected virtual int RepeatCount(ChaosLevel level) => 1;

    protected virtual TimeSpan RepeatDelay =>
        TimeSpan.FromMilliseconds(120);

    public async Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        if (context.RequireDestinyForeground &&
            !ForegroundWindowService.IsDestinyForeground())
        {
            return;
        }

        int repeats = Math.Max(
            1,
            RepeatCount(context.SelectedLevel));

        for (int index = 0; index < repeats; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (int deltaX, int deltaY) = GetMovement(
                context.SelectedLevel,
                context.Random);

            await context.Input.MoveMouseSmoothAsync(
                deltaX,
                deltaY,
                MovementDuration(context.SelectedLevel),
                cancellationToken);

            if (index < repeats - 1)
            {
                await Task.Delay(
                    RepeatDelay,
                    cancellationToken);
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
            ChaosLevel.Normal =>
                TimeSpan.FromMilliseconds(250),

            ChaosLevel.Chaos =>
                TimeSpan.FromMilliseconds(550),

            _ =>
                TimeSpan.FromMilliseconds(150)
        };
}

public abstract class DirectionalLookEffectBase : MouseMoveChaosEffectBase
{
    protected abstract int DirectionX { get; }
    protected abstract int DirectionY { get; }

    protected override (int DeltaX, int DeltaY) GetMovement(
        ChaosLevel level,
        Random random)
    {
        int amount = level switch
        {
            ChaosLevel.Low => 110,
            ChaosLevel.Normal => 230,
            ChaosLevel.Chaos => 420,
            _ => 150
        };

        return (
            DirectionX * amount,
            DirectionY * amount);
    }
}

public sealed class LookLeftEffect : DirectionalLookEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "look_left",
        Name = "Look Left",
        Description = "Moves the mouse left to turn the camera.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Low,
        Weight = 9,
        DurationSeconds = 1,
        CooldownSeconds = 45,
        CanStack = true
    };

    protected override int DirectionX => -1;
    protected override int DirectionY => 0;
}

public sealed class LookRightEffect : DirectionalLookEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "look_right",
        Name = "Look Right",
        Description = "Moves the mouse right to turn the camera.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Low,
        Weight = 9,
        DurationSeconds = 1,
        CooldownSeconds = 45,
        CanStack = true
    };

    protected override int DirectionX => 1;
    protected override int DirectionY => 0;
}

public sealed class LookUpEffect : DirectionalLookEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "look_up",
        Name = "Look Up",
        Description = "Moves the mouse upward to move the camera.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 7,
        DurationSeconds = 1,
        CooldownSeconds = 55,
        CanStack = true
    };

    protected override int DirectionX => 0;
    protected override int DirectionY => -1;
}

public sealed class LookDownEffect : DirectionalLookEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "look_down",
        Name = "Look Down",
        Description = "Moves the mouse downward to move the camera.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 7,
        DurationSeconds = 1,
        CooldownSeconds = 55,
        CanStack = true
    };

    protected override int DirectionX => 0;
    protected override int DirectionY => 1;
}

public sealed class CameraJoltEffect : MouseMoveChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "camera_jolt",
        Name = "Camera Jolt",
        Description = "Jerks the camera in random directions.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 8,
        DurationSeconds = 2,
        CooldownSeconds = 75,
        CanStack = true
    };

    protected override (int DeltaX, int DeltaY) GetMovement(
        ChaosLevel level,
        Random random)
    {
        int minimum = level switch
        {
            ChaosLevel.Normal => 100,
            ChaosLevel.Chaos => 180,
            _ => 70
        };

        int maximum = level switch
        {
            ChaosLevel.Normal => 230,
            ChaosLevel.Chaos => 420,
            _ => 140
        };

        int deltaX = random.Next(minimum, maximum + 1) *
                     (random.Next(2) == 0 ? -1 : 1);

        int deltaY = random.Next(minimum / 2, maximum / 2 + 1) *
                     (random.Next(2) == 0 ? -1 : 1);

        return (deltaX, deltaY);
    }

    protected override int RepeatCount(ChaosLevel level) =>
        level == ChaosLevel.Chaos ? 3 : 2;

    protected override TimeSpan MovementDuration(ChaosLevel level) =>
        level == ChaosLevel.Chaos
            ? TimeSpan.FromMilliseconds(100)
            : TimeSpan.FromMilliseconds(140);

    protected override TimeSpan RepeatDelay =>
        TimeSpan.FromMilliseconds(90);
}
