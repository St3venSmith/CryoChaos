using System.Runtime.InteropServices;
using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Effects;

/// <summary>
/// Base for an effect that repeatedly presses a key chosen in code. Derive
/// from this class and set VirtualKey, count, and delay; reflection discovers
/// the concrete effect automatically.
/// </summary>
public abstract class RepeatingKeyChaosEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }
    protected abstract ushort VirtualKey { get; }
    protected virtual int PressCount(ChaosLevel level) => 8;
    protected virtual TimeSpan DelayBetweenPresses => TimeSpan.FromMilliseconds(350);
    protected virtual TimeSpan HoldDuration => TimeSpan.FromMilliseconds(45);

    public async Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken)
    {
        if (!await FocusDestinyAsync(cancellationToken))
        {
            return;
        }

        int count = Math.Clamp(PressCount(context.SelectedLevel), 1, 100);
        for (int index = 0; index < count; index++)
        {
            await context.Input.PressKeyboardAsync(VirtualKey, HoldDuration, cancellationToken);
            if (index < count - 1)
            {
                await Task.Delay(DelayBetweenPresses, cancellationToken);
            }
        }
    }

    internal static async Task<bool> FocusDestinyAsync(CancellationToken cancellationToken)
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
}

public abstract class SoundChaosEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }
    protected abstract string Sound { get; }
    protected virtual ChaosSoundSource Source => ChaosSoundSource.WindowsAlias;
    protected virtual int RepeatCount(ChaosLevel level) => 1;
    protected virtual TimeSpan RepeatDelay => TimeSpan.FromMilliseconds(500);
    protected virtual bool Loop => false;

    public Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken) =>
        context.SoundEffects.PlayAsync(
            Sound,
            Source,
            RepeatCount(context.SelectedLevel),
            RepeatDelay,
            Loop,
            context.GetEffectDuration(Definition),
            cancellationToken);
}

public abstract class QteChaosEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }
    protected virtual IReadOnlyList<ushort> AllowedKeys =>
        Enumerable.Range(0x30, 10) // 0-9
            .Concat(Enumerable.Range(0x41, 26)) // A-Z
            .Concat(Enumerable.Range(0x70, 12)) // F1-F12
            .Concat([0x20, 0x25, 0x26, 0x27, 0x28]) // Space and arrows
            .Select(value => (ushort)value)
            .ToArray();
    protected virtual int PromptCount(ChaosLevel level) => level == ChaosLevel.Chaos ? 6 : 4;
    protected virtual TimeSpan TimePerPrompt(ChaosLevel level) =>
        level == ChaosLevel.Chaos ? TimeSpan.FromMilliseconds(2500) : TimeSpan.FromMilliseconds(2400);
    protected virtual int FailureEffectCount => 2;

    public async Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken)
    {
        if (!await RepeatingKeyChaosEffectBase.FocusDestinyAsync(cancellationToken))
        {
            return;
        }

        int prompts = PromptCount(context.SelectedLevel);
        TimeSpan promptTime = TimePerPrompt(context.SelectedLevel);
        HashSet<ushort> movementKeys = ResolveMovementBindings(context)
            .Select(binding => binding.VirtualKey)
            .ToHashSet();
        ushort[] usableKeys = AllowedKeys
            .Where(key => key != 0 && !movementKeys.Contains(key))
            .Distinct()
            .ToArray();
        if (usableKeys.Length == 0)
        {
            throw new InvalidOperationException("No non-movement QTE keys are available.");
        }

        bool success = await RunWhileMovementLockedAsync(
            context,
            promptTime * prompts + TimeSpan.FromSeconds(2),
            token => context.Qte.RunAsync(
                new QteOptions(usableKeys, prompts, promptTime, Definition.Name),
                context.Random,
                token),
            cancellationToken);

        if (!success)
        {
            context.QueueRandomEffects(FailureEffectCount);
        }
    }

    internal static async Task<T> RunWhileMovementLockedAsync<T>(
        ChaosEffectContext context,
        TimeSpan maximumDuration,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        List<InputBinding> movementBindings = ResolveMovementBindings(context);

        using CancellationTokenSource movementCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task movementLock = context.InputRemapper.SuppressAsync(
            movementBindings,
            maximumDuration,
            movementCancellation.Token);

        try
        {
            return await action(cancellationToken);
        }
        finally
        {
            movementCancellation.Cancel();
            try
            {
                await movementLock;
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static List<InputBinding> ResolveMovementBindings(ChaosEffectContext context)
    {
        string[] movementAliases =
        [
            "move_forward", "forward", "key_move_forward",
            "move_backward", "move_back", "backward", "key_move_backward",
            "move_left", "strafe_left", "left", "key_move_left",
            "move_right", "strafe_right", "right", "key_move_right"
        ];

        return context.Keybinds
            .ResolveActionBindings(movementAliases)
            .Where(binding => binding.Kind == InputBindingKind.Keyboard)
            .Concat(new[]
            {
                KeyboardBinding(0x57, "default forward"),
                KeyboardBinding(0x41, "default left"),
                KeyboardBinding(0x53, "default backward"),
                KeyboardBinding(0x44, "default right")
            })
            .DistinctBy(binding => binding.VirtualKey)
            .ToList();
    }

    private static InputBinding KeyboardBinding(ushort virtualKey, string name) => new()
    {
        RawValue = name,
        Kind = InputBindingKind.Keyboard,
        VirtualKey = virtualKey
    };
}

public sealed class RandomQteEffect : QteChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "random_qte",
        Name = "Quick Time Panic",
        Description = "Complete a random QTE. Failure triggers two random effects.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 5,
        DurationSeconds = 9,
        CooldownSeconds = 75,
        CanStack = false
    };
}

public sealed class WarningSoundEffect : SoundChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "warning_sound", Name = "System Panic",
        Description = "Plays a repeated warning sound.", Type = ChaosEffectType.Graphic,
        MinimumLevel = ChaosLevel.Low, Weight = 5, DurationSeconds = 4,
        CooldownSeconds = 45, CanStack = true
    };
    protected override string Sound => "SystemHand";
    protected override int RepeatCount(ChaosLevel level) => 4;
    protected override TimeSpan RepeatDelay => TimeSpan.FromMilliseconds(600);
}

public abstract class MathChallengeEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }
    protected virtual int FailureEffectCount => 2;
    protected virtual int HardQuestionChance => 100;
    protected virtual TimeSpan EasyTimeLimit => TimeSpan.FromSeconds(12);
    protected virtual TimeSpan HardTimeLimit => TimeSpan.FromSeconds(25);

    public async Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken)
    {
        if (!await RepeatingKeyChaosEffectBase.FocusDestinyAsync(cancellationToken))
        {
            return;
        }

        bool hard = context.Random.Next(Math.Max(1, HardQuestionChance)) == 0;
        (string question, int answer) = CreateQuestion(context.Random, hard);
        TimeSpan timeLimit = context.ScaleDuration(
            hard ? HardTimeLimit : EasyTimeLimit);
        bool success = await QteChaosEffectBase.RunWhileMovementLockedAsync(
            context,
            timeLimit + TimeSpan.FromSeconds(2),
            token => context.Qte.RunMathAsync(
                question,
                answer,
                timeLimit,
                () =>
                {
                    if (!context.TryTriggerRandomEffectNow())
                    {
                        context.QueueRandomEffects(1);
                    }
                },
                token),
            cancellationToken);

        if (!success)
        {
            context.QueueRandomEffects(FailureEffectCount);
        }
    }

    protected virtual (string Question, int Answer) CreateQuestion(Random random, bool hard)
    {
        if (!hard)
        {
            int first = random.Next(2, 31);
            int second = random.Next(2, 31);
            return ($"{first} + {second} = ?", first + second);
        }

        int coefficient = random.Next(2, 10);
        int answer = random.Next(3, 16);
        int offset = random.Next(2, 25);
        int result = coefficient * answer + offset;
        return ($"Solve x:  {coefficient}x + {offset} = {result}", answer);
    }
}

public sealed class MathPanicEffect : MathChallengeEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "math_panic", Name = "Math Panic",
        Description = "Solve while movement is locked; every wrong answer triggers another effect.",
        Type = ChaosEffectType.Keybind, MinimumLevel = ChaosLevel.Normal,
        Weight = 5, DurationSeconds = 14, CooldownSeconds = 80, CanStack = true
    };
}

public abstract class JumpScareChaosEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }
    protected virtual string ScareText => "☠";
    protected virtual string Sound => "SystemHand";
    protected virtual ChaosSoundSource SoundSource => ChaosSoundSource.WindowsAlias;
    protected virtual TimeSpan ScareDuration => TimeSpan.FromMilliseconds(1200);

    public async Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken)
    {
        TimeSpan duration = context.ScaleDuration(ScareDuration);
        Task sound = context.SoundEffects.PlayAsync(
            Sound, SoundSource, 1, TimeSpan.Zero, true,
            duration, cancellationToken);
        Task visual = context.Qte.ShowJumpScareAsync(
            ScareText, duration, cancellationToken);
        await Task.WhenAll(sound, visual);
    }
}

public sealed class JumpScareEffect : JumpScareChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "jump_scare", Name = "Jump Scare",
        Description = "Flashes a full-screen scare with a loud warning sound.",
        Type = ChaosEffectType.Graphic, MinimumLevel = ChaosLevel.Chaos,
        Weight = 3, DurationSeconds = 2, CooldownSeconds = 120, CanStack = false
    };
}

[ChaosEffectTemplate]
public sealed class YourRepeatingKeyEffect : RepeatingKeyChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "your_repeating_key", Name = "Your Repeating Key",
        Description = "Template: repeatedly presses R.", Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Low, Weight = 1, DurationSeconds = 5,
        CooldownSeconds = 30, CanStack = true
    };
    protected override ushort VirtualKey => 0x52; // R
    protected override int PressCount(ChaosLevel level) => 10;
    protected override TimeSpan DelayBetweenPresses => TimeSpan.FromMilliseconds(250);
}

[ChaosEffectTemplate]
public sealed class YourSoundEffect : SoundChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "your_sound", Name = "Your Sound", Description = "Template sound effect.",
        Type = ChaosEffectType.Graphic, MinimumLevel = ChaosLevel.Low, Weight = 1,
        DurationSeconds = 4, CooldownSeconds = 30, CanStack = true
    };
    protected override string Sound => "SystemExclamation";
    protected override int RepeatCount(ChaosLevel level) => 3;
}

[ChaosEffectTemplate]
public sealed class YourQteEffect : QteChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "your_qte", Name = "Your QTE", Description = "Template customizable QTE.",
        Type = ChaosEffectType.Keybind, MinimumLevel = ChaosLevel.Low, Weight = 1,
        DurationSeconds = 8, CooldownSeconds = 30, CanStack = false
    };
    protected override IReadOnlyList<ushort> AllowedKeys => [0x51, 0x45, 0x52, 0x46];
    protected override int PromptCount(ChaosLevel level) => 5;
}

public sealed class WeaponJammedEffect : IChaosEffect
{
    public ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "disable_fire", Name = "Weapon Jammed",
        Description = "Every shot immediately forces a reload.", Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal, Weight = 7, DurationSeconds = 16,
        CooldownSeconds = 50, CanStack = true
    };

    public async Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken)
    {
        if (!await RepeatingKeyChaosEffectBase.FocusDestinyAsync(cancellationToken))
        {
            return;
        }

        InputBinding reload = context.Keybinds.ResolveActionForSimulation(
            "reload", "weapon_reload", "key_reload") ?? new InputBinding
            {
                RawValue = "default reload", Kind = InputBindingKind.Keyboard, VirtualKey = 0x52
            };
        IReadOnlyList<InputBinding> detected = context.Keybinds.ResolveActionBindings(
            "fire", "attack", "primary_fire", "shoot", "key_fire");
        int[] fireKeys = detected.Select(ToVirtualKey).Where(key => key != 0)
            .Append(0x01).Distinct().ToArray();
        bool wasDown = false;
        DateTimeOffset end = DateTimeOffset.UtcNow.Add(context.GetEffectDuration(Definition));

        while (DateTimeOffset.UtcNow < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool isDown = fireKeys.Any(key => (GetAsyncKeyState(key) & 0x8000) != 0);
            if (isDown && !wasDown)
            {
                await Task.Delay(75, cancellationToken);
                await context.Input.PressAsync(reload, TimeSpan.FromMilliseconds(45), cancellationToken);
            }
            wasDown = isDown;
            await Task.Delay(8, cancellationToken);
        }
    }

    private static int ToVirtualKey(InputBinding binding) => binding.Kind switch
    {
        InputBindingKind.Keyboard => binding.VirtualKey,
        InputBindingKind.MouseButton => binding.MouseButton switch
        {
            MouseInputButton.Left => 0x01, MouseInputButton.Right => 0x02,
            MouseInputButton.Middle => 0x04, MouseInputButton.XButton1 => 0x05,
            MouseInputButton.XButton2 => 0x06, _ => 0
        },
        _ => 0
    };

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}

public abstract class WeaponLockEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }
    protected abstract string[] ActionAliases { get; }
    protected abstract ushort DefaultVirtualKey { get; }

    public async Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken)
    {
        if (!await RepeatingKeyChaosEffectBase.FocusDestinyAsync(cancellationToken))
        {
            return;
        }

        InputBinding binding = context.Keybinds.ResolveActionForSimulation(ActionAliases) ??
            new InputBinding
            {
                RawValue = "default weapon slot",
                Kind = InputBindingKind.Keyboard,
                VirtualKey = DefaultVirtualKey
            };
        DateTimeOffset end = DateTimeOffset.UtcNow.Add(context.GetEffectDuration(Definition));
        while (DateTimeOffset.UtcNow < end)
        {
            await context.Input.PressAsync(
                binding,
                TimeSpan.FromMilliseconds(35),
                cancellationToken);
            await Task.Delay(215, cancellationToken);
        }
    }
}

public sealed class PrimaryWeaponLockEffect : WeaponLockEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "primary_weapon_lock", Name = "Primary Weapon Lock",
        Description = "Continuously selects the primary weapon.", Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal, Weight = 6, DurationSeconds = 12,
        CooldownSeconds = 55, CanStack = true
    };
    protected override string[] ActionAliases =>
        ["weapon_1", "select_primary_weapon", "primary_weapon", "switch_to_primary_weapon"];
    protected override ushort DefaultVirtualKey => 0x31;
}

public sealed class SpecialWeaponLockEffect : WeaponLockEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "special_weapon_lock", Name = "Special Weapon Lock",
        Description = "Continuously selects the special weapon.", Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal, Weight = 5, DurationSeconds = 12,
        CooldownSeconds = 55, CanStack = true
    };
    protected override string[] ActionAliases =>
        ["weapon_2", "select_special_weapon", "special_weapon", "secondary_weapon"];
    protected override ushort DefaultVirtualKey => 0x32;
}

public sealed class HeavyWeaponLockEffect : WeaponLockEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "heavy_weapon_lock", Name = "Heavy Weapon Lock",
        Description = "Continuously selects the heavy weapon.", Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos, Weight = 4, DurationSeconds = 12,
        CooldownSeconds = 60, CanStack = true
    };
    protected override string[] ActionAliases =>
        ["weapon_3", "select_heavy_weapon", "heavy_weapon", "switch_to_heavy_weapon"];
    protected override ushort DefaultVirtualKey => 0x33;
}
