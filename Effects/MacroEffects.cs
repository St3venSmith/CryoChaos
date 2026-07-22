using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Effects;

public abstract record InputMacroStep;

public sealed record MacroMouseMove(
    int DeltaX,
    int DeltaY,
    int DurationMilliseconds) : InputMacroStep;

public sealed record MacroPressAction(
    string[] ActionAliases,
    int HoldMilliseconds = 75,
    ushort FallbackVirtualKey = 0) : InputMacroStep;

public sealed record MacroPressMouseButton(
    MouseInputButton Button,
    int HoldMilliseconds = 75) : InputMacroStep;

public sealed record MacroHoldTogether(
    string[][] ActionAliasGroups,
    int HoldMilliseconds) : InputMacroStep;

public sealed record MacroDelay(
    int Milliseconds) : InputMacroStep;

/// <summary>
/// Executes a sequence of mouse moves, detected Destiny actions, simultaneous
/// action groups, and delays. Derived effects only need to declare Steps.
/// </summary>
public abstract class MacroChaosEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }
    protected abstract IReadOnlyList<InputMacroStep> Steps { get; }

    public async Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        if (!ForegroundWindowService.IsDestinyForeground())
        {
            if (!ForegroundWindowService.TryActivateDestinyWindow())
            {
                return;
            }

            await Task.Delay(150, cancellationToken);
            if (!ForegroundWindowService.IsDestinyForeground())
            {
                return;
            }
        }

        foreach (InputMacroStep step in Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            switch (step)
            {
                case MacroMouseMove move:
                    await context.Input.MoveMouseSmoothAsync(
                        move.DeltaX,
                        move.DeltaY,
                        TimeSpan.FromMilliseconds(move.DurationMilliseconds),
                        cancellationToken);
                    break;

                case MacroPressAction press:
                    InputBinding binding = ResolveAction(
                        context,
                        press.ActionAliases,
                        press.FallbackVirtualKey);
                    await context.Input.PressAsync(
                        binding,
                        TimeSpan.FromMilliseconds(press.HoldMilliseconds),
                        cancellationToken);
                    break;

                case MacroPressMouseButton mouseButton:
                    await context.Input.PressMouseButtonAsync(
                        mouseButton.Button,
                        TimeSpan.FromMilliseconds(mouseButton.HoldMilliseconds),
                        cancellationToken);
                    break;

                case MacroHoldTogether together:
                    InputBinding[] bindings = together.ActionAliasGroups
                        .Select(aliases => ResolveRequiredAction(context, aliases))
                        .ToArray();
                    await context.Input.PressTogetherAsync(
                        bindings,
                        TimeSpan.FromMilliseconds(together.HoldMilliseconds),
                        cancellationToken);
                    break;

                case MacroDelay delay:
                    await Task.Delay(delay.Milliseconds, cancellationToken);
                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported macro step: {step.GetType().Name}");
            }
        }
    }

    private static InputBinding ResolveRequiredAction(
        ChaosEffectContext context,
        string[] aliases) =>
        context.Keybinds.ResolveActionForSimulation(aliases) ??
        throw new InvalidOperationException(
            $"No detected Destiny binding matched: {string.Join(", ", aliases)}");

    private static InputBinding ResolveAction(
        ChaosEffectContext context,
        string[] aliases,
        ushort fallbackVirtualKey)
    {
        InputBinding? detected = context.Keybinds.ResolveActionForSimulation(aliases);
        if (detected is not null)
        {
            return detected;
        }

        if (fallbackVirtualKey != 0)
        {
            return new InputBinding
            {
                RawValue = $"macro fallback 0x{fallbackVirtualKey:X2}",
                Kind = InputBindingKind.Keyboard,
                VirtualKey = fallbackVirtualKey
            };
        }

        throw new InvalidOperationException(
            $"No detected Destiny binding matched: {string.Join(", ", aliases)}");
    }
}

public sealed class HeavyDropShotEffect : MacroChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "heavy_drop_shot",
        Name = "Heavy Drop Shot",
        Description = "Looks down, selects the heavy weapon, then fires.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 4,
        DurationSeconds = 3,
        CooldownSeconds = 120,
        CanStack = false
    };

    protected override IReadOnlyList<InputMacroStep> Steps { get; } =
    [
        new MacroMouseMove(0, 1200, 120),
        new MacroDelay(100),
        new MacroPressAction(
            ["weapon_3", "select_heavy_weapon", "heavy_weapon", "switch_to_heavy_weapon"],
            HoldMilliseconds: 100,
            FallbackVirtualKey: 0x33),
        new MacroDelay(250),
        new MacroPressMouseButton(MouseInputButton.Left, 120)
    ];
}
