using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Effects;

/// <summary>
/// Template base for live Destiny audio effects. Add a concrete class with a
/// Definition and Mode; ChaosEngine discovers it automatically.
/// </summary>
public abstract class GameAudioChaosEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }
    protected abstract GameAudioEffectMode Mode { get; }

    public Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken) =>
        context.GameAudioEffects.PlayAsync(
            Mode,
            context.GetEffectDuration(Definition),
            cancellationToken);
}

public sealed class AudioEchoEffect : GameAudioChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = Define(
        "audio_echo", "Echo Chamber", "Adds repeating echoes to Destiny's live audio.", 12, 65);
    protected override GameAudioEffectMode Mode => GameAudioEffectMode.Echo;
    private static ChaosEffectDefinition Define(string id, string name, string description, int duration, int cooldown) =>
        AudioEffectDefinitions.Create(id, name, description, duration, cooldown);
}

public sealed class ReverseAudioEffect : GameAudioChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = AudioEffectDefinitions.Create(
        "audio_reverse", "Backmasked", "Plays Destiny's live audio in short reversed windows.", 10, 75, ChaosLevel.Chaos);
    protected override GameAudioEffectMode Mode => GameAudioEffectMode.Reverse;
}

public sealed class RadioAudioEffect : GameAudioChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = AudioEffectDefinitions.Create(
        "audio_radio", "Bad Radio", "Crushes the game audio through a narrow, noisy radio filter.", 13, 60);
    protected override GameAudioEffectMode Mode => GameAudioEffectMode.Radio;
}

public sealed class UnderwaterAudioEffect : GameAudioChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = AudioEffectDefinitions.Create(
        "audio_underwater", "Underwater", "Heavily muffles Destiny's live audio.", 14, 60);
    protected override GameAudioEffectMode Mode => GameAudioEffectMode.Underwater;
}

public sealed class PitchUpAudioEffect : GameAudioChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = AudioEffectDefinitions.Create(
        "audio_pitch_up", "Helium Mix", "Raises the pitch of Destiny's live audio.", 11, 70);
    protected override GameAudioEffectMode Mode => GameAudioEffectMode.PitchUp;
}

public sealed class PitchDownAudioEffect : GameAudioChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = AudioEffectDefinitions.Create(
        "audio_pitch_down", "Demon Mix", "Lowers the pitch of Destiny's live audio.", 11, 70);
    protected override GameAudioEffectMode Mode => GameAudioEffectMode.PitchDown;
}

public sealed class ReverbAudioEffect : GameAudioChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = AudioEffectDefinitions.Create(
        "audio_reverb", "Infinite Hall", "Adds a dense artificial reverb to Destiny's audio.", 14, 65);
    protected override GameAudioEffectMode Mode => GameAudioEffectMode.Reverb;
}

public sealed class StaticAudioEffect : GameAudioChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = AudioEffectDefinitions.Create(
        "audio_static", "Dead Air", "Mixes random static bursts into Destiny's audio.", 12, 55);
    protected override GameAudioEffectMode Mode => GameAudioEffectMode.RandomStatic;
}

internal static class AudioEffectDefinitions
{
    public static ChaosEffectDefinition Create(
        string id,
        string name,
        string description,
        int duration,
        int cooldown,
        ChaosLevel minimumLevel = ChaosLevel.Normal) => new()
        {
            Id = id,
            Name = name,
            Description = description,
            Type = ChaosEffectType.Audio,
            MinimumLevel = minimumLevel,
            Weight = 10,
            DurationSeconds = duration,
            CooldownSeconds = cooldown,
            CanStack = false
        };
}
