using System.Windows.Media;
using CryoChaos.Models;

namespace CryoChaos.Effects;

public sealed class RedTintEffect : IChaosEffect
{
    public ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "red_tint",
        Name = "Red Alert",
        Description = "Covers the screen with a red tint.",
        Type = ChaosEffectType.Graphic,
        MinimumLevel = ChaosLevel.Low,
        Weight = 15,
        DurationSeconds = 8,
        CooldownSeconds = 45,
        CanStack = true
    };

    public Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken)
    {
        double opacity = context.SelectedLevel switch
        {
            ChaosLevel.Low => 0.14,
            ChaosLevel.Normal => 0.24,
            _ => 0.36
        };

        return context.Overlay.ShowTintAsync(
            Definition.Id,
            Color.FromRgb(210, 20, 35),
            opacity,
            context.GetEffectDuration(Definition),
            cancellationToken);
    }
}

public sealed class TunnelVisionEffect : IChaosEffect
{
    public ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "tunnel_vision",
        Name = "Tunnel Vision",
        Description = "Darkens the outside of the screen.",
        Type = ChaosEffectType.Graphic,
        MinimumLevel = ChaosLevel.Low,
        Weight = 12,
        DurationSeconds = 10,
        CooldownSeconds = 60,
        CanStack = true
    };

    public Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken)
    {
        double openingScale = context.SelectedLevel switch
        {
            ChaosLevel.Low => 0.72,
            ChaosLevel.Normal => 0.52,
            _ => 0.34
        };

        return context.Overlay.ShowTunnelVisionAsync(
            Definition.Id,
            openingScale,
            context.GetEffectDuration(Definition),
            cancellationToken);
    }
}

public sealed class BlackoutPulseEffect : IChaosEffect
{
    public ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "blackout_pulse",
        Name = "Blink",
        Description = "Briefly makes the screen completely black.",
        Type = ChaosEffectType.Graphic,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 9,
        DurationSeconds = 7,
        CooldownSeconds = 80,
        CanStack = true
    };

    public Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken)
    {
        double peakOpacity = 1.0;
        int pulseCount = context.SelectedLevel == ChaosLevel.Chaos ? 5 : 3;

        return context.Overlay.ShowBlackoutPulseAsync(
            Definition.Id,
            peakOpacity,
            pulseCount,
            context.GetEffectDuration(Definition),
            cancellationToken);
    }
}

public sealed class MovingBlockEffect : IChaosEffect
{
    public ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "moving_block",
        Name = "Screen Intruder",
        Description = "Moves an opaque block across the screen.",
        Type = ChaosEffectType.Graphic,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 7,
        DurationSeconds = 12,
        CooldownSeconds = 100,
        CanStack = true
    };

    public Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken)
    {
        return context.Overlay.ShowMovingBlockAsync(
            Definition.Id,
            context.GetEffectDuration(Definition),
            cancellationToken);
    }
}
