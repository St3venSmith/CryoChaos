using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Effects;

public abstract class ExpansionMouseEffectBase : RawMouseChaosEffectBase
{
    private readonly ChaosEffectDefinition _definition;
    private readonly RawMouseEffectMode _mode;
    private readonly int _outputLimit;

    protected ExpansionMouseEffectBase(
        string id,
        string name,
        string description,
        RawMouseEffectMode mode,
        int outputLimit = 70)
    {
        _mode = mode;
        _outputLimit = outputLimit;
        _definition = new ChaosEffectDefinition
        {
            Id = id,
            Name = name,
            Description = description,
            Type = ChaosEffectType.Keybind,
            MinimumLevel = ChaosLevel.Normal,
            Weight = 10,
            DurationSeconds = 14,
            CooldownSeconds = 65,
            CanStack = false
        };
    }

    public override ChaosEffectDefinition Definition => _definition;
    protected override RawMouseEffectMode Mode => _mode;
    protected override int BaseOutputLimit => _outputLimit;
}

public sealed class MouseBuoyancyEffect : ExpansionMouseEffectBase
{
    public MouseBuoyancyEffect() : base(
        "raw_mouse_buoyancy", "Aim Buoyancy",
        "A gentle upward current continuously lifts the camera.",
        RawMouseEffectMode.Buoyancy, 40) { }
}

public sealed class MouseWobbleEffect : ExpansionMouseEffectBase
{
    public MouseWobbleEffect() : base(
        "raw_mouse_wobble", "Unsteady Hands",
        "Adds a small smooth wobble while leaving the mouse controllable.",
        RawMouseEffectMode.AimWobble, 45) { }
}

public sealed class StickyHorizontalMouseEffect : ExpansionMouseEffectBase
{
    public StickyHorizontalMouseEffect() : base(
        "raw_sticky_horizontal", "Sticky X Axis",
        "Horizontal aiming resists movement while vertical aim stays normal.",
        RawMouseEffectMode.StickyHorizontal, 110) { }
}

public sealed class StickyVerticalMouseEffect : ExpansionMouseEffectBase
{
    public StickyVerticalMouseEffect() : base(
        "raw_sticky_vertical", "Sticky Y Axis",
        "Vertical aiming resists movement while horizontal aim stays normal.",
        RawMouseEffectMode.StickyVertical, 110) { }
}

public sealed class PulsingSensitivityMouseEffect : ExpansionMouseEffectBase
{
    public PulsingSensitivityMouseEffect() : base(
        "raw_sensitivity_pulse", "Sensitivity Tide",
        "Sensitivity slowly breathes between 0.82x and 1.18x.",
        RawMouseEffectMode.SensitivityPulse, 80) { }
}

public sealed class MouseEchoEffect : ExpansionMouseEffectBase
{
    public MouseEchoEffect() : base(
        "raw_mouse_echo", "Aim Echo",
        "A restrained delayed echo follows quick camera movement.",
        RawMouseEffectMode.MouseEcho, 85) { }
}

public sealed class MouseRatchetEffect : ExpansionMouseEffectBase
{
    public MouseRatchetEffect() : base(
        "raw_mouse_ratchet", "Notched Aim",
        "Camera motion advances in small five-count mechanical notches.",
        RawMouseEffectMode.Ratchet, 100) { }
}

public sealed class MouseQuicksandEffect : ExpansionMouseEffectBase
{
    public MouseQuicksandEffect() : base(
        "raw_mouse_quicksand", "Aim Quicksand",
        "Slow movement is resisted more than fast movement.",
        RawMouseEffectMode.Quicksand, 100) { }
}
