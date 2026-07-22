using CryoChaos.Models;

namespace CryoChaos.Effects;

public abstract class MutatorChaosEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }

    protected TimeSpan Duration(ChaosEffectContext context) =>
        context.GetEffectDuration(Definition);

    public abstract Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken);
}

public sealed class DoubleDurationMutatorEffect : MutatorChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = MutatorDefinitions.Create(
        "mutator_double_duration", "Double Time",
        "Effects started during this mutator last twice as long.", 18, 90);

    public override Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken) =>
        context.Mutators.DoubleDurationAsync(Duration(context), cancellationToken);
}

public sealed class TripleDurationMutatorEffect : MutatorChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = MutatorDefinitions.Create(
        "mutator_triple_duration", "Triple Time",
        "Effects started during this mutator last three times as long.", 15, 120,
        ChaosLevel.Chaos);

    public override Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken) =>
        context.Mutators.TripleDurationAsync(Duration(context), cancellationToken);
}

public sealed class DoubleStackMutatorEffect : MutatorChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = MutatorDefinitions.Create(
        "mutator_double_stack", "Two for One",
        "Every new ordinary effect immediately starts one extra stackable effect.", 20, 110,
        ChaosLevel.Chaos);

    public override Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken) =>
        context.Mutators.DuplicateNewEffectsAsync(Duration(context), cancellationToken);
}

public sealed class PurgeEffectsMutatorEffect : MutatorChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = MutatorDefinitions.Create(
        "mutator_purge_effects", "Clean Slate",
        "Immediately cancels every other active chaos effect.", 3, 80);

    public override async Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        context.Mutators.RemoveAllOtherEffects(context.CurrentEffectId);
        await Task.Delay(Duration(context), cancellationToken);
    }
}

public sealed class ProtectionMutatorEffect : MutatorChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = MutatorDefinitions.Create(
        "mutator_protection", "Chaos Shield",
        "Clears current effects and blocks new ordinary effects for a short time.", 14, 130,
        ChaosLevel.Normal);

    public override Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        context.Mutators.RemoveAllOtherEffects(context.CurrentEffectId);
        return context.Mutators.ProtectAsync(Duration(context), cancellationToken);
    }
}

internal static class MutatorDefinitions
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
            Type = ChaosEffectType.Mutator,
            MinimumLevel = minimumLevel,
            Weight = 10,
            DurationSeconds = duration,
            CooldownSeconds = cooldown,
            CanStack = true
        };
}
