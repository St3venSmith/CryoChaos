using System.Windows.Media;
using CryoChaos.Models;

namespace CryoChaos.Effects;

public sealed class YourGraphicEffect : IChaosEffect
{
    public ChaosEffectDefinition Definition { get; } = new()
    {
        // Must be unique.
        Id = "your_graphic_effect",

        // Displayed on the side of the screen.
        Name = "Purple Screen",

        Description =
            "Covers the screen with a purple tint.",

        Type = ChaosEffectType.Graphic,

        // Low = available in Low, Normal, and Chaos.
        MinimumLevel = ChaosLevel.Low,

        // Higher weight means it is selected more often.
        Weight = 10,

        // Default display duration.
        DurationSeconds = 6,

        // Time before it can be selected again.
        CooldownSeconds = 60,

        CanStack = true
    };

    public Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        double opacity = context.SelectedLevel switch
        {
            ChaosLevel.Low => 0.25,
            ChaosLevel.Normal => 0.50,
            ChaosLevel.Chaos => 0.80,
            _ => 0.25
        };

        TimeSpan duration = context.SelectedLevel switch
        {
            ChaosLevel.Low =>
                TimeSpan.FromSeconds(4),

            ChaosLevel.Normal =>
                TimeSpan.FromSeconds(6),

            ChaosLevel.Chaos =>
                TimeSpan.FromSeconds(9),

            _ =>
                TimeSpan.FromSeconds(
                    Definition.DurationSeconds)
        };

        return context.Overlay.ShowTintAsync(
            Definition.Id,
            Colors.Purple,
            opacity,
            duration,
            cancellationToken);
    }
}