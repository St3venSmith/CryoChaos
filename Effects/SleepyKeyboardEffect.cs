using CryoChaos.Models;

namespace CryoChaos.Effects;

public sealed class SleepyKeyboardEffect : IChaosEffect
{
    public ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "sleepy_keyboard",
        Name = "Sleepy Keyboard",
        Description = "Delays every keyboard input before it reaches the game.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 10,
        DurationSeconds = 18,
        CooldownSeconds = 70,
        CanStack = false
    };

    public Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        double delayMilliseconds = context.SelectedLevel switch
        {
            ChaosLevel.Low => 140,
            ChaosLevel.Normal => 220,
            ChaosLevel.Chaos => 320,
            _ => 220
        };

        return context.InputRemapper.DelayKeyboardAsync(
            TimeSpan.FromMilliseconds(delayMilliseconds),
            context.GetEffectDuration(Definition),
            cancellationToken);
    }
}
