using CryoChaos.Models;
using CryoChaos.Services;
using CryoChaos.Views;

namespace CryoChaos.Effects;

public sealed class ChaosEffectContext
{
    public required ChaosLevel SelectedLevel { get; init; }
    public required OverlayWindow Overlay { get; init; }
    public required DestinyKeybindService Keybinds { get; init; }
    public required KeyboardInputService Input { get; init; }
    public required KeyboardRemapService InputRemapper { get; init; }
    public required RawMouseEffectService RawMouseEffects { get; init; }
    public required SoundEffectService SoundEffects { get; init; }
    public required GameAudioEffectService GameAudioEffects { get; init; }
    public required VideoOverlayService VideoOverlay { get; init; }
    public required QteService Qte { get; init; }
    public required ChaosMutatorService Mutators { get; init; }
    public required string CurrentEffectId { get; init; }
    public required double RuntimeDurationMultiplier { get; init; }
    public required Action<int> QueueRandomEffects { get; init; }
    public required Func<bool> TryTriggerRandomEffectNow { get; init; }
    public required IScreenTransformService ScreenTransform { get; init; }
    public required Random Random { get; init; }
    public required bool RequireDestinyForeground { get; init; }

    public double DurationMultiplier => GetDurationMultiplier(SelectedLevel);

    public static double GetDurationMultiplier(ChaosLevel level) => level switch
    {
        ChaosLevel.Low => 1.0,
        ChaosLevel.Normal => 1.3,
        ChaosLevel.Chaos => 1.75,
        _ => 1.0
    };

    public TimeSpan ScaleDuration(TimeSpan baseDuration) =>
        TimeSpan.FromMilliseconds(baseDuration.TotalMilliseconds * DurationMultiplier);

    public TimeSpan ScaleEffectDuration(TimeSpan baseDuration) =>
        TimeSpan.FromMilliseconds(
            ScaleDuration(baseDuration).TotalMilliseconds *
            RuntimeDurationMultiplier);

    public TimeSpan GetEffectDuration(ChaosEffectDefinition definition) =>
        ScaleEffectDuration(TimeSpan.FromSeconds(definition.DurationSeconds));
}

public interface IChaosEffect
{
    ChaosEffectDefinition Definition { get; }

    Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Marks a concrete example class that should compile but should not be loaded
/// as a playable effect. Remove this attribute from a copied template.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ChaosEffectTemplateAttribute : Attribute;
