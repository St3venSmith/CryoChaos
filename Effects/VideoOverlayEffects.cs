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

    public Task RunAsync(ChaosEffectContext context, CancellationToken cancellationToken) =>
        context.VideoOverlay.ShowAsync(
            new VideoOverlayOptions(
                VideoPath,
                VideoOpacity,
                Volume,
                VideoStretch,
                Loop,
                context.GetEffectDuration(Definition)),
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
}
