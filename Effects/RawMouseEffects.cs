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

public sealed class MouseMomentumEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = PhysicsDefinition(
        "raw_mouse_momentum", "Aim Momentum",
        "Mouse movement keeps gliding after the physical mouse stops.", 16, 65);
    protected override RawMouseEffectMode Mode => RawMouseEffectMode.Momentum;
    protected override int BaseOutputLimit => 300;
    private static ChaosEffectDefinition PhysicsDefinition(string id, string name, string description, int duration, int cooldown) =>
        MousePhysicsDefinitions.Create(id, name, description, duration, cooldown);
}

public sealed class MouseElasticEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = MousePhysicsDefinitions.Create(
        "raw_mouse_elastic", "Rubber Aim",
        "Camera movement overshoots and springs back like elastic.", 15, 70);
    protected override RawMouseEffectMode Mode => RawMouseEffectMode.Elastic;
    protected override int BaseOutputLimit => 340;
}

public sealed class MouseGravityEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = MousePhysicsDefinitions.Create(
        "raw_mouse_gravity", "Heavy Crosshair",
        "A constant downward force drags the camera.", 16, 60);
    protected override RawMouseEffectMode Mode => RawMouseEffectMode.Gravity;
    protected override int BaseOutputLimit => 120;
}

public sealed class MouseMagnetEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = MousePhysicsDefinitions.Create(
        "raw_mouse_magnet", "Magnetic Aim",
        "Camera movement is pulled back toward its starting center.", 15, 70);
    protected override RawMouseEffectMode Mode => RawMouseEffectMode.Magnet;
    protected override int BaseOutputLimit => 260;
}

public sealed class MouseOrbitEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = MousePhysicsDefinitions.Create(
        "raw_mouse_orbit", "Orbital Aim",
        "The camera is continuously pushed around a circular path.", 14, 75,
        ChaosLevel.Chaos);
    protected override RawMouseEffectMode Mode => RawMouseEffectMode.Orbit;
    protected override int BaseOutputLimit => 120;
}

public sealed class MouseDeadzoneEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = MousePhysicsDefinitions.Create(
        "raw_mouse_deadzone", "Twenty Pixel Tax",
        "The first 20 counts of each mouse movement are ignored.", 16, 65);
    protected override RawMouseEffectMode Mode => RawMouseEffectMode.Deadzone;
    protected override int BaseOutputLimit => 280;
}

public sealed class MouseWindEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = MousePhysicsDefinitions.Create(
        "raw_mouse_wind", "Crosswind",
        "Smooth random gusts push the camera in changing directions.", 16, 60);
    protected override RawMouseEffectMode Mode => RawMouseEffectMode.Wind;
    protected override int BaseOutputLimit => 140;
}

public sealed class MouseFrictionEffect : RawMouseChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = MousePhysicsDefinitions.Create(
        "raw_mouse_friction", "Breaking Loose",
        "Mouse movement starts extremely slow and accelerates over time.", 18, 65);
    protected override RawMouseEffectMode Mode => RawMouseEffectMode.Friction;
    protected override int BaseOutputLimit => 300;
}

internal static class MousePhysicsDefinitions
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
            Type = ChaosEffectType.Keybind,
            MinimumLevel = minimumLevel,
            Weight = 10,
            DurationSeconds = duration,
            CooldownSeconds = cooldown,
            CanStack = false
        };
}
