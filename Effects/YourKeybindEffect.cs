using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Effects;

/// <summary>
/// Template for an effect that triggers one of the player's
/// detected Destiny 2 keybinds.
/// </summary>
public sealed class YourKeybindEffect :
    KeybindChaosEffectBase
{
    public override ChaosEffectDefinition Definition
    {
        get;
    } = new()
    {
        // Must be unique and should use lowercase with underscores.
        Id = "your_keybind_effect",

        // Displayed on the current-effect overlay.
        Name = "Your Keybind Effect",

        Description =
            "Describe what this effect does.",

        Type = ChaosEffectType.Keybind,

        // Low effects work in Low, Normal, and Chaos.
        // Normal effects work in Normal and Chaos.
        // Chaos effects work only in Chaos.
        MinimumLevel = ChaosLevel.Low,

        // Higher numbers make the effect more likely.
        Weight = 10,

        // How long the effect appears in the active-effect display.
        DurationSeconds = 1,

        // How long before this effect can be selected again.
        CooldownSeconds = 60,

        // True allows it to run alongside another effect.
        CanStack = true
    };

    /// <summary>
    /// Use the exact action name found in Destiny's cvars.xml.
    ///
    /// Example:
    /// <cvar name="reload" value="r!unused" />
    ///
    /// The action name would be "reload".
    /// </summary>
    protected override string[] ActionAliases =>
    [
        "exact_cvars_action_name",

        // Optional alternate names:
        "alternate_action_name"
    ];

    /// <summary>
    /// Controls how many times the input is pressed.
    /// </summary>
    protected override int RepeatCount(
        ChaosLevel level)
    {
        return level switch
        {
            ChaosLevel.Low => 1,
            ChaosLevel.Normal => 1,
            ChaosLevel.Chaos => 2,
            _ => 1
        };
    }

    /// <summary>
    /// Controls how long each key or mouse button is held.
    /// </summary>
    protected override TimeSpan HoldDuration(
        ChaosLevel level)
    {
        return level switch
        {
            ChaosLevel.Low =>
                TimeSpan.FromMilliseconds(75),

            ChaosLevel.Normal =>
                TimeSpan.FromMilliseconds(100),

            ChaosLevel.Chaos =>
                TimeSpan.FromMilliseconds(150),

            _ =>
                TimeSpan.FromMilliseconds(75)
        };
    }

    /// <summary>
    /// Delay between repeated presses.
    /// </summary>
    protected override TimeSpan RepeatDelay =>
        TimeSpan.FromMilliseconds(250);
}