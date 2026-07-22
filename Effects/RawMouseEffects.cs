using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Effects;

public abstract class RawMouseChaosEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }
    protected abstract RawMouseEffectMode Mode { get; }
    protected virtual double SensitivityMultiplier => 1.0;
    protected virtual int BaseOutputLimit => 180;

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

            await Task.Delay(140, cancellationToken);
            if (!ForegroundWindowService.IsDestinyForeground())
            {
                return;
            }
        }

        await context.RawMouseEffects.RunAsync(
            Mode,
            SensitivityMultiplier,
            BaseOutputLimit,
            context.GetEffectDuration(Definition),
            cancellationToken);
    }
}

public sealed class DisableLookUpRawEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "raw_disable_look_up",
        Name = "Ceiling Denied",
        Description = "Cancels upward physical Raw Input mouse movement.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 4,
        DurationSeconds = 12,
        CooldownSeconds = 75,
        CanStack = false
    };

    protected override RawMouseEffectMode Mode =>
        RawMouseEffectMode.CancelLookUp;
}

public sealed class DisableLookDownRawEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "raw_disable_look_down",
        Name = "Floor Denied",
        Description = "Cancels downward physical Raw Input mouse movement.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 4,
        DurationSeconds = 12,
        CooldownSeconds = 75,
        CanStack = false
    };

    protected override RawMouseEffectMode Mode =>
        RawMouseEffectMode.CancelLookDown;
}

public sealed class DisableVerticalLookRawEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "raw_disable_vertical_look",
        Name = "Vertical Lock",
        Description = "Cancels all vertical physical Raw Input mouse movement.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 3,
        DurationSeconds = 10,
        CooldownSeconds = 90,
        CanStack = false
    };

    protected override RawMouseEffectMode Mode =>
        RawMouseEffectMode.CancelVertical;
}

public sealed class DisableHorizontalLookRawEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "raw_disable_horizontal_look",
        Name = "Horizontal Lock",
        Description = "Cancels all horizontal physical Raw Input mouse movement.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 3,
        DurationSeconds = 10,
        CooldownSeconds = 90,
        CanStack = false
    };

    protected override RawMouseEffectMode Mode =>
        RawMouseEffectMode.CancelHorizontal;
}

public sealed class FastMouseSensitivityEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "raw_fast_sensitivity",
        Name = "Hyper Sensitivity",
        Description = "Raises physical mouse sensitivity to 1.75x.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 5,
        DurationSeconds = 14,
        CooldownSeconds = 70,
        CanStack = false
    };

    protected override RawMouseEffectMode Mode =>
        RawMouseEffectMode.Scale;
    protected override double SensitivityMultiplier => 1.75;
    protected override int BaseOutputLimit => 240;
}

public sealed class SlowMouseSensitivityEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "raw_slow_sensitivity",
        Name = "Sluggish Aim",
        Description = "Lowers physical mouse sensitivity to 0.45x.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 5,
        DurationSeconds = 14,
        CooldownSeconds = 70,
        CanStack = false
    };

    protected override RawMouseEffectMode Mode =>
        RawMouseEffectMode.Scale;
    protected override double SensitivityMultiplier => 0.45;
    protected override int BaseOutputLimit => 240;
}

public sealed class InvertHorizontalCameraEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "raw_invert_horizontal", Name = "Reverse X Camera",
        Description = "Inverts left and right physical camera movement.",
        Type = ChaosEffectType.Keybind, MinimumLevel = ChaosLevel.Normal,
        Weight = 4, DurationSeconds = 14, CooldownSeconds = 75, CanStack = false
    };
    protected override RawMouseEffectMode Mode => RawMouseEffectMode.InvertHorizontal;
    protected override int BaseOutputLimit => 360;
}

public sealed class InvertVerticalCameraEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "raw_invert_vertical", Name = "Reverse Y Camera",
        Description = "Inverts up and down physical camera movement.",
        Type = ChaosEffectType.Keybind, MinimumLevel = ChaosLevel.Normal,
        Weight = 4, DurationSeconds = 14, CooldownSeconds = 75, CanStack = false
    };
    protected override RawMouseEffectMode Mode => RawMouseEffectMode.InvertVertical;
    protected override int BaseOutputLimit => 360;
}

public sealed class InvertCameraEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "raw_invert_camera", Name = "Inside-Out Aim",
        Description = "Inverts both camera axes.", Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos, Weight = 3, DurationSeconds = 12,
        CooldownSeconds = 90, CanStack = false
    };
    protected override RawMouseEffectMode Mode => RawMouseEffectMode.InvertBoth;
    protected override int BaseOutputLimit => 420;
}
