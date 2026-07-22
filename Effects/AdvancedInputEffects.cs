using CryoChaos.Models;
using CryoChaos.Services;
using CryoChaos.Views;

namespace CryoChaos.Effects;

internal static class MovementBindings
{
    private static readonly string[][] DirectionAliases =
    [
        ["move_forward", "forward", "key_move_forward"],
        ["move_left", "strafe_left", "left", "key_move_left"],
        ["move_backward", "move_back", "backward", "key_move_backward"],
        ["move_right", "strafe_right", "right", "key_move_right"]
    ];

    private static readonly ushort[] DefaultDirectionKeys =
        [0x57, 0x41, 0x53, 0x44]; // W, A, S, D

    public static IReadOnlyList<InputBinding> ResolveAll(
        ChaosEffectContext context)
    {
        List<InputBinding> bindings = [];

        foreach (string[] aliases in DirectionAliases)
        {
            bindings.AddRange(context.Keybinds.ResolveActionBindings(aliases));
        }

        bindings.AddRange(
            context.Keybinds.ResolveActionBindings(
                "jump", "key_jump", "move_jump"));
        bindings.AddRange(
            context.Keybinds.ResolveActionBindings(
                "crouch", "toggle_crouch", "hold_crouch", "key_crouch"));
        bindings.AddRange(
            context.Keybinds.ResolveActionBindings(
                "sprint", "hold_sprint", "toggle_sprint", "key_sprint"));

        // Default controls remain fallbacks if Destiny uses an unexpected
        // cvars action name. Detected custom bindings are blocked as well.
        bindings.AddRange(DefaultDirectionKeys.Select(CreateKeyboardBinding));
        bindings.Add(CreateKeyboardBinding(0x20)); // Space
        bindings.Add(CreateKeyboardBinding(0xA0)); // Left Shift
        bindings.Add(CreateKeyboardBinding(0xA1)); // Right Shift
        bindings.Add(CreateKeyboardBinding(0xA2)); // Left Ctrl
        bindings.Add(CreateKeyboardBinding(0xA3)); // Right Ctrl

        return bindings
            .DistinctBy(binding =>
                (binding.Kind,
                 binding.VirtualKey,
                 binding.MouseButton,
                 binding.WheelDirection))
            .ToArray();
    }

    public static async Task<bool> EnsureDestinyForegroundAsync(
        CancellationToken cancellationToken)
    {
        if (ForegroundWindowService.IsDestinyForeground())
        {
            return true;
        }

        if (!ForegroundWindowService.TryActivateDestinyWindow())
        {
            return false;
        }

        await Task.Delay(140, cancellationToken);
        return ForegroundWindowService.IsDestinyForeground();
    }

    public static IReadOnlyList<ushort> ResolveDirectionKeys(
        ChaosEffectContext context)
    {
        ushort[] keys = DirectionAliases
            .Select((aliases, index) =>
                context.Keybinds
                    .ResolveActionBindings(aliases)
                    .FirstOrDefault(binding =>
                        binding.Kind == InputBindingKind.Keyboard)
                    ?.VirtualKey ?? DefaultDirectionKeys[index])
            .ToArray();

        // A malformed or duplicated cvars mapping cannot form a permutation;
        // fall back to literal WASD in that case.
        return keys.Distinct().Count() == keys.Length
            ? keys
            : DefaultDirectionKeys;
    }

    private static InputBinding CreateKeyboardBinding(ushort virtualKey) =>
        new()
        {
            RawValue = $"movement fallback 0x{virtualKey:X2}",
            Kind = InputBindingKind.Keyboard,
            VirtualKey = virtualKey
        };
}

public sealed class DisableAllMovementEffect : IChaosEffect
{
    public ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "disable_all_movement",
        Name = "Frozen Solid",
        Description = "Disables movement, sprint, crouch, and jump inputs temporarily.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 6,
        DurationSeconds = 12,
        CooldownSeconds = 65,
        CanStack = true
    };

    public async Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        if (!await MovementBindings.EnsureDestinyForegroundAsync(
                cancellationToken))
        {
            return;
        }

        await context.InputRemapper.SuppressAsync(
            MovementBindings.ResolveAll(context),
            context.GetEffectDuration(Definition),
            cancellationToken);
    }
}

public sealed class RandomizeMovementEffect : IChaosEffect
{
    public ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "randomize_movement",
        Name = "Scrambled WASD",
        Description = "Randomly rearranges forward, left, backward, and right controls.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 5,
        DurationSeconds = 14,
        CooldownSeconds = 75,
        CanStack = true
    };

    public async Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        if (!await MovementBindings.EnsureDestinyForegroundAsync(
                cancellationToken))
        {
            return;
        }

        ushort[] source = MovementBindings
            .ResolveDirectionKeys(context)
            .ToArray();
        ushort[] target = CreateDerangement(source, context.Random);

        Dictionary<ushort, ushort> mapping = source
            .Select((key, index) => (key, target: target[index]))
            .ToDictionary(pair => pair.key, pair => pair.target);

        await context.InputRemapper.RemapKeyboardAsync(
            mapping,
            context.GetEffectDuration(Definition),
            cancellationToken);
    }

    private static ushort[] CreateDerangement(
        IReadOnlyList<ushort> source,
        Random random)
    {
        ushort[] shuffled = source.ToArray();

        do
        {
            random.Shuffle(shuffled);
        }
        while (shuffled.Where((key, index) => key == source[index]).Any());

        return shuffled;
    }
}

public sealed class UnskippableAdEffect : IChaosEffect
{
    private static readonly TimeSpan SkipDelay = TimeSpan.FromSeconds(4);

    public ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "unskippable_ad",
        Name = "Unskippable Ad",
        Description = "Blocks movement until a four-second ad finishes and Skip is clicked.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 3,
        DurationSeconds = 4,
        CooldownSeconds = 180,
        CanStack = false
    };

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

            await Task.Delay(120, cancellationToken);
        }

        using CancellationTokenSource suppressionCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        Task suppression = context.InputRemapper.SuppressAsync(
            MovementBindings.ResolveAll(context),
            Timeout.InfiniteTimeSpan,
            suppressionCancellation.Token);

        try
        {
            Task startupDelay = Task.Delay(80, cancellationToken);
            Task startupResult = await Task.WhenAny(suppression, startupDelay);
            if (startupResult == suppression)
            {
                await suppression;
            }

            await ChaosAdWindow.ShowAsync(SkipDelay, cancellationToken);
        }
        finally
        {
            suppressionCancellation.Cancel();

            try
            {
                await suppression;
            }
            catch (OperationCanceledException)
            {
            }

            ForegroundWindowService.TryActivateDestinyWindow();
        }
    }
}
