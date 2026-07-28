using CryoChaos.Models;

namespace CryoChaos.Effects;

public abstract class ExpansionScreenEffectBase : ScreenTransformEffectBase
{
    private readonly ChaosEffectDefinition _definition;
    private readonly ScreenTransformMode _mode;

    protected ExpansionScreenEffectBase(
        string id,
        string name,
        string description,
        ScreenTransformMode mode,
        ChaosLevel minimumLevel = ChaosLevel.Normal,
        int duration = 9,
        int cooldown = 95)
    {
        _mode = mode;
        _definition = new ChaosEffectDefinition
        {
            Id = id,
            Name = name,
            Description = description,
            Type = ChaosEffectType.ScreenTransform,
            MinimumLevel = minimumLevel,
            Weight = 1,
            DurationSeconds = duration,
            CooldownSeconds = cooldown,
            CanStack = false
        };
    }

    public override ChaosEffectDefinition Definition => _definition;
    protected override ScreenTransformMode TransformMode => _mode;
}

public sealed class VhsTrackingScreenEffect : ExpansionScreenEffectBase
{
    public VhsTrackingScreenEffect() : base(
        "vhs_tracking", "VHS Tracking",
        "Adds rolling tracking faults, tape noise, and unstable color alignment.",
        ScreenTransformMode.VhsTracking) { }
}

public sealed class DoubleVisionScreenEffect : ExpansionScreenEffectBase
{
    public DoubleVisionScreenEffect() : base(
        "double_vision", "Double Vision",
        "Offsets a translucent second view that sways across the original.",
        ScreenTransformMode.DoubleVision) { }
}

public sealed class RadialRushScreenEffect : ExpansionScreenEffectBase
{
    public RadialRushScreenEffect() : base(
        "radial_rush", "Warp Speed",
        "Pulls the image outward in a fast radial rush.",
        ScreenTransformMode.RadialRush, ChaosLevel.Chaos, 8, 110) { }
}

public sealed class WaterRippleScreenEffect : ExpansionScreenEffectBase
{
    public WaterRippleScreenEffect() : base(
        "water_ripple", "Liquid Monitor",
        "Sends overlapping circular ripples through the captured game.",
        ScreenTransformMode.WaterRipple) { }
}

public sealed class SecurityCameraScreenEffect : ExpansionScreenEffectBase
{
    public SecurityCameraScreenEffect() : base(
        "security_camera", "Security Feed",
        "Turns the view into a noisy monochrome surveillance feed.",
        ScreenTransformMode.SecurityCamera, ChaosLevel.Low) { }
}

public sealed class ComicInkScreenEffect : ExpansionScreenEffectBase
{
    public ComicInkScreenEffect() : base(
        "comic_ink", "Ink Outline",
        "Quantizes color and draws dark outlines around image detail.",
        ScreenTransformMode.ComicInk) { }
}

public sealed class PrismLensScreenEffect : ExpansionScreenEffectBase
{
    public PrismLensScreenEffect() : base(
        "prism_lens", "Prism Lens",
        "Splits colors tangentially through a rotating glass prism.",
        ScreenTransformMode.PrismLens, ChaosLevel.Chaos) { }
}

public sealed class RollingShutterScreenEffect : ExpansionScreenEffectBase
{
    public RollingShutterScreenEffect() : base(
        "rolling_shutter", "Rolling Shutter",
        "Makes horizontal rows lag behind each other like a damaged camera.",
        ScreenTransformMode.RollingShutter) { }
}

public sealed class FrostedGlassScreenEffect : ExpansionScreenEffectBase
{
    public FrostedGlassScreenEffect() : base(
        "frosted_glass", "Frosted Glass",
        "Breaks the view into shimmering translucent glass cells.",
        ScreenTransformMode.FrostedGlass) { }
}

public sealed class SolarizeScreenEffect : ExpansionScreenEffectBase
{
    public SolarizeScreenEffect() : base(
        "solarize", "Solar Flare",
        "Folds bright colors back into an alien solarized palette.",
        ScreenTransformMode.Solarize) { }
}

public sealed class CrtCurvatureScreenEffect : ExpansionScreenEffectBase
{
    public CrtCurvatureScreenEffect() : base(
        "crt_curvature", "Tube Television",
        "Warps the image onto a curved CRT with a soft phosphor grille.",
        ScreenTransformMode.CrtCurvature) { }
}

public sealed class MosaicShuffleScreenEffect : ExpansionScreenEffectBase
{
    public MosaicShuffleScreenEffect() : base(
        "mosaic_shuffle", "Broken Mosaic",
        "Rearranges screen tiles into a moving digital mosaic.",
        ScreenTransformMode.MosaicShuffle, ChaosLevel.Chaos, 8, 115) { }
}

public sealed class HyperspaceScreenEffect : ExpansionScreenEffectBase
{
    public HyperspaceScreenEffect() : base(
        "hyperspace", "Hyperspace",
        "Stretches bright image detail into animated star-like trails.",
        ScreenTransformMode.Hyperspace, ChaosLevel.Chaos, 8, 115) { }
}

public sealed class ColorBlindShuffleScreenEffect : ExpansionScreenEffectBase
{
    public ColorBlindShuffleScreenEffect() : base(
        "channel_shuffle", "Channel Roulette",
        "Continuously rotates and remaps the live image color channels.",
        ScreenTransformMode.ColorBlindShuffle) { }
}

public sealed class TunnelVisionScreenEffect : ExpansionScreenEffectBase
{
    public TunnelVisionScreenEffect() : base(
        "shader_tunnel_vision", "Warp Tunnel",
        "Keeps the center sharp while the outer view twists into darkness.",
        ScreenTransformMode.TunnelVision, ChaosLevel.Chaos, 9, 110) { }
}
