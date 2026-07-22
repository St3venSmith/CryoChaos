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
        // Manual triggers give focus to the CryoChaos window. Activate
        // Destiny before installing the hook instead of announcing the effect
        // and silently returning without suppressing anything.
        if (!ForegroundWindowService.IsDestinyForeground())
        {
            if (!ForegroundWindowService.TryActivateDestinyWindow())
            {
                return;
            }

            await Task.Delay(140, cancellationToken);
            if (!ForegroundWindowService.IsDestinyForeground())
            {
                return;
            }
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
        // Relative SendInput movement only turns Destiny's camera while the
        // game owns the foreground/raw-input target. Manual or scheduled
        // effects can fire while the CryoChaos control panel has focus, so
        // return focus to Destiny before sending movement.
        if (!ForegroundWindowService.IsDestinyForeground())
        {
            if (!ForegroundWindowService.TryActivateDestinyWindow())
            {
                return;
            }

            await Task.Delay(
                TimeSpan.FromMilliseconds(100),
                cancellationToken);

            if (!ForegroundWindowService.IsDestinyForeground())
            {
                return;
            }
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

/// <summary>
/// Base class for effects that hold multiple detected bindings together.
/// Each inner alias array describes one required Destiny action.
/// </summary>
public abstract class MultiInputChaosEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }
    protected abstract string[][] ActionAliasGroups { get; }

    protected virtual TimeSpan HoldDuration(ChaosLevel level) =>
        level switch
        {
            ChaosLevel.Low => TimeSpan.FromMilliseconds(350),
            ChaosLevel.Normal => TimeSpan.FromMilliseconds(650),
            ChaosLevel.Chaos => TimeSpan.FromMilliseconds(1000),
            _ => TimeSpan.FromMilliseconds(500)
        };

    public async Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        // Clicking Trigger now gives focus to CryoChaos. Return focus to the
        // game before sending the simultaneous inputs.
        if (!ForegroundWindowService.IsDestinyForeground())
        {
            if (!ForegroundWindowService.TryActivateDestinyWindow())
            {
                return;
            }

            await Task.Delay(140, cancellationToken);
            if (!ForegroundWindowService.IsDestinyForeground())
            {
                return;
            }
        }

        InputBinding?[] resolved = ActionAliasGroups
            .Select(context.Keybinds.ResolveActionForSimulation)
            .ToArray();

        if (resolved.Any(binding => binding is null))
        {
            return;
        }

        await context.Input.PressTogetherAsync(
            resolved.Cast<InputBinding>(),
            HoldDuration(context.SelectedLevel),
            cancellationToken);
    }
}

/// <summary>
/// Base class for timed, bidirectional swaps between two detected keyboard
/// actions, such as forward &lt;-&gt; backward or left &lt;-&gt; right.
/// </summary>
public abstract class InputSwapChaosEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }
    protected abstract string[] FirstActionAliases { get; }
    protected abstract string[] SecondActionAliases { get; }

    protected virtual TimeSpan SwapDuration(ChaosLevel level) =>
        TimeSpan.FromSeconds(Definition.DurationSeconds);

    public async Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        if (context.RequireDestinyForeground &&
            !ForegroundWindowService.IsDestinyForeground())
        {
            return;
        }

        InputBinding? first =
            context.Keybinds.ResolveActionForSimulation(FirstActionAliases);
        InputBinding? second =
            context.Keybinds.ResolveActionForSimulation(SecondActionAliases);

        if (first is null || second is null)
        {
            return;
        }

        await context.InputRemapper.SwapAsync(
            first,
            second,
            context.ScaleDuration(SwapDuration(context.SelectedLevel)),
            cancellationToken);
    }
}

/// <summary>
/// Temporarily suppresses one detected keyboard or mouse-button action while
/// Destiny is foreground.
/// </summary>
public abstract class InputDisableChaosEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }
    protected abstract string[] ActionAliases { get; }
    protected virtual IReadOnlyList<InputBinding> AdditionalBindings => [];

    protected virtual TimeSpan DisableDuration(ChaosLevel level) =>
        TimeSpan.FromSeconds(Definition.DurationSeconds);

    public async Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        // Clicking Trigger now gives focus to CryoChaos. Return focus to the
        // game before installing the suppression hook so the effect cannot be
        // announced while silently doing nothing.
        if (!ForegroundWindowService.IsDestinyForeground())
        {
            if (!ForegroundWindowService.TryActivateDestinyWindow())
            {
                return;
            }

            await Task.Delay(140, cancellationToken);
            if (!ForegroundWindowService.IsDestinyForeground())
            {
                return;
            }
        }

        IReadOnlyList<InputBinding> detectedBindings = context.Keybinds
            .ResolveActionBindings(ActionAliases);

        List<InputBinding> bindings = detectedBindings
            .Concat(AdditionalBindings)
            .DistinctBy(binding => new
            {
                binding.Kind,
                binding.VirtualKey,
                binding.MouseButton,
                binding.WheelDirection
            })
            .ToList();

        // Low-level mouse suppression is not dependable for games that read
        // Raw Input. Never announce a disable effect that cannot actually
        // suppress the player's binding. Substitute another random effect if
        // either the detected action or its explicit fallback uses a mouse
        // button or wheel.
        if (bindings.Any(binding =>
                binding.Kind is InputBindingKind.MouseButton or
                    InputBindingKind.MouseWheel))
        {
            context.QueueRandomEffects(1);
            return;
        }

        if (bindings.Count == 0)
        {
            throw new InvalidOperationException(
                $"No detected binding matched: {string.Join(", ", ActionAliases)}");
        }

        await context.InputRemapper.SuppressAsync(
            bindings,
            context.ScaleDuration(DisableDuration(context.SelectedLevel)),
            cancellationToken);
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
        CanStack = true
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

public sealed class ForwardRightEffect : MultiInputChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "forward_right",
        Name = "Diagonal Rush",
        Description = "Holds the detected forward and right movement bindings together.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 8,
        DurationSeconds = 2,
        CooldownSeconds = 65,
        CanStack = true
    };

    protected override string[][] ActionAliasGroups =>
    [
        ["move_forward", "forward", "key_move_forward"],
        ["move_right", "strafe_right", "right", "key_move_right"]
    ];
}

public sealed class ReverseMovementEffect : InputSwapChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "reverse_movement",
        Name = "Reverse Movement",
        Description = "Temporarily swaps the detected forward and backward keys.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 5,
        DurationSeconds = 12,
        CooldownSeconds = 70,
        CanStack = true
    };

    protected override string[] FirstActionAliases =>
        ["move_forward", "forward", "key_move_forward"];

    protected override string[] SecondActionAliases =>
        ["move_backward", "move_back", "backward", "key_move_backward"];
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
            ChaosLevel.Low => 650,
            ChaosLevel.Normal => 1100,
            ChaosLevel.Chaos => 1800,
            _ => 800
        };

        return (
            DirectionX * amount,
            DirectionY * amount);
    }

    protected override int RepeatCount(ChaosLevel level) =>
        level == ChaosLevel.Chaos ? 2 : 1;

    protected override TimeSpan MovementDuration(ChaosLevel level) =>
        level == ChaosLevel.Chaos
            ? TimeSpan.FromMilliseconds(55)
            : TimeSpan.FromMilliseconds(75);
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
