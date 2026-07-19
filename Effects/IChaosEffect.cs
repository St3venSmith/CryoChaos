using CryoChaos.Models;
using CryoChaos.Services;
using CryoChaos.Views;

namespace CryoChaos.Effects;

public sealed class ChaosEffectContext
{
    public required ChaosLevel SelectedLevel { get; init; }
    public required OverlayWindow Overlay { get; init; }
    public required DestinyKeybindService Keybinds { get; init; }
    public required KeyboardInputService Input { get; init; }
    public required IScreenTransformService ScreenTransform { get; init; }
    public required Random Random { get; init; }
    public required bool RequireDestinyForeground { get; init; }
}

public interface IChaosEffect
{
    ChaosEffectDefinition Definition { get; }

    Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken);
}
