using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Effects;

public abstract class ScreenTransformEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }

    protected abstract ScreenTransformMode TransformMode { get; }

    protected virtual TimeSpan GetDuration(ChaosLevel level)
    {
        return TimeSpan.FromSeconds(Definition.DurationSeconds);
    }

    public Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        if (context.RequireDestinyForeground &&
            !ForegroundWindowService.IsDestinyForeground())
        {
            return Task.CompletedTask;
        }

        return context.ScreenTransform.ShowAsync(
            TransformMode,
            GetDuration(context.SelectedLevel),
            cancellationToken);
    }
}

public sealed class UpsideDownEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "upside_down",
        Name = "Upside Down",
        Description = "Rotates the live game image by 180 degrees.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 7,
        DurationSeconds = 8,
        CooldownSeconds = 120,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.Rotate180;
}

public sealed class SidewaysLeftEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "sideways_left",
        Name = "Sideways Left",
        Description = "Rotates the live game image 90 degrees counterclockwise.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 5,
        DurationSeconds = 8,
        CooldownSeconds = 150,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.Rotate90CounterClockwise;
}

public sealed class SidewaysRightEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "sideways_right",
        Name = "Sideways Right",
        Description = "Rotates the live game image 90 degrees clockwise.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 5,
        DurationSeconds = 8,
        CooldownSeconds = 150,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.Rotate90Clockwise;
}

public sealed class MirrorScreenEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "mirror_screen",
        Name = "Mirror Mode",
        Description = "Flips the live game image from left to right.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 8,
        DurationSeconds = 9,
        CooldownSeconds = 110,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.FlipHorizontal;
}

public sealed class VerticalFlipEffect : ScreenTransformEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "vertical_flip",
        Name = "Vertical Flip",
        Description = "Flips the live game image from top to bottom.",
        Type = ChaosEffectType.ScreenTransform,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 5,
        DurationSeconds = 8,
        CooldownSeconds = 140,
        CanStack = false
    };

    protected override ScreenTransformMode TransformMode =>
        ScreenTransformMode.FlipVertical;
}
