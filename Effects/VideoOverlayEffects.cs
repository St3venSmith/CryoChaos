using System.Windows.Media;
using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Effects;

/// <summary>
/// Base for topmost, non-activating, click-through video overlays. The window
/// background is transparent and VideoOpacity controls how strongly the video
/// covers the game. Video audio remains enabled through Volume.
/// </summary>
public abstract class VideoOverlayChaosEffectBase : IChaosEffect
{
    public abstract ChaosEffectDefinition Definition { get; }
    protected abstract string VideoPath { get; }
    protected virtual double VideoOpacity => 0.78;
    protected virtual double Volume => 1.0;
    protected virtual Stretch VideoStretch => Stretch.Uniform;
    protected virtual bool Loop => false;
    protected virtual bool TransparentBackground => true;

    public Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken) =>
        context.VideoOverlay.ShowAsync(
            new VideoOverlayOptions(
                VideoPath,
                VideoOpacity,
                Volume,
                VideoStretch,
                Loop,
                context.GetEffectDuration(Definition),
                TransparentBackground),
            cancellationToken);
}

/// <summary>
/// Copy this class, give it a unique ID/name, set VideoPath, and remove
/// ChaosEffectTemplate to make it appear in the effect library.
/// </summary>
[ChaosEffectTemplate]
public sealed class YourVideoOverlayEffect : VideoOverlayChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "your_video_overlay",
        Name = "Your Video Overlay",
        Description = "Template click-through video overlay with audio.",
        Type = ChaosEffectType.Graphic,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 2,
        DurationSeconds = 10,
        CooldownSeconds = 90,
        CanStack = false
    };

    protected override string VideoPath => "Videos/your-video.mp4";
    protected override double VideoOpacity => 0.72;
    protected override double Volume => 1.0;
    protected override Stretch VideoStretch => Stretch.Uniform;
    protected override bool Loop => false;
    protected override bool TransparentBackground => true;
}

/// <summary>
/// Full-screen fake error-code video. Put the source video at
/// Videos/fake-error-code.mp4; it is copied beside the built application.
/// </summary>
public sealed class FakeErrorCodeVideoEffect : VideoOverlayChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "fake_error_code_video",
        Name = "Fake Error Code",
        Description = "Covers Destiny with a click-through fake error-code video and its audio.",
        Type = ChaosEffectType.Graphic,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 35,
        DurationSeconds = 15,
        CooldownSeconds = 150,
        CanStack = false
    };

    protected override string VideoPath =>
        "Videos/fake-error-code.mp4";
    protected override double VideoOpacity => 1.0;
    protected override double Volume => 1.0;
    protected override Stretch VideoStretch =>
        Stretch.UniformToFill;
    protected override bool Loop => false;
    protected override bool TransparentBackground => false;
}
