using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Effects;

public abstract class ExpansionDisableBase : InputDisableChaosEffectBase
{
    private readonly ChaosEffectDefinition _definition;
    private readonly string[] _aliases;
    private readonly IReadOnlyList<InputBinding> _fallbacks;

    protected ExpansionDisableBase(
        string id,
        string name,
        string description,
        string[] aliases,
        IReadOnlyList<InputBinding> fallbacks)
    {
        _aliases = aliases;
        _fallbacks = fallbacks;
        _definition = ExpansionDefinitions.Create(
            id, name, description, ChaosLevel.Normal, 15, 55, true);
    }

    public override ChaosEffectDefinition Definition => _definition;
    protected override string[] ActionAliases => _aliases;
    protected override IReadOnlyList<InputBinding> AdditionalBindings => _fallbacks;
}

public sealed class DisableInventoryEffect : ExpansionDisableBase
{
    public DisableInventoryEffect() : base(
        "disable_inventory", "Inventory Locked",
        "Disables every inventory binding detected in the player's cvars.xml.",
        [
            "inventory",
            "open_inventory",
            "ui_open_inventory",
            "character_screen",
            "open_character",
            "ui_open_character",
            "key_inventory"
        ],
        []) { }
}

public abstract class ExpansionSoundBase : SoundChaosEffectBase
{
    private readonly ChaosEffectDefinition _definition;
    private readonly string _sound;
    private readonly int _repeats;

    protected ExpansionSoundBase(
        string id, string name, string description, string sound, int repeats)
    {
        _sound = sound;
        _repeats = repeats;
        _definition = ExpansionDefinitions.Create(
            id, name, description, ChaosLevel.Low, 3, 35, true,
            ChaosEffectType.Audio);
    }

    public override ChaosEffectDefinition Definition => _definition;
    protected override string Sound => _sound;
    protected override int RepeatCount(ChaosLevel level) => _repeats;
    protected override TimeSpan RepeatDelay => TimeSpan.FromMilliseconds(420);
}

public sealed class ExclamationSoundEffect : ExpansionSoundBase
{
    public ExclamationSoundEffect() : base(
        "sound_exclamation", "Incoming!", "Plays a sharp system exclamation.",
        "SystemExclamation", 2) { }
}

public sealed class QuestionSoundEffect : ExpansionSoundBase
{
    public QuestionSoundEffect() : base(
        "sound_question", "Confusion Chime", "Plays a questioning two-note interruption.",
        "SystemQuestion", 2) { }
}

public sealed class NotificationBurstSoundEffect : ExpansionSoundBase
{
    public NotificationBurstSoundEffect() : base(
        "sound_notification_burst", "Notification Storm",
        "Plays a short burst of notification chimes.", "SystemAsterisk", 4) { }
}

public sealed class CriticalPulseSoundEffect : ExpansionSoundBase
{
    public CriticalPulseSoundEffect() : base(
        "sound_critical_pulse", "Critical Pulse",
        "Plays three spaced critical warning tones.", "SystemHand", 3) { }
}

public sealed class DefaultBeepSoundEffect : ExpansionSoundBase
{
    public DefaultBeepSoundEffect() : base(
        "sound_default_beep", "Retro Beeps", "Plays a compact sequence of classic system beeps.",
        "SystemDefault", 5) { }
}

internal static class ExpansionDefinitions
{
    public static ChaosEffectDefinition Create(
        string id,
        string name,
        string description,
        ChaosLevel level,
        int duration,
        int cooldown,
        bool canStack,
        ChaosEffectType type = ChaosEffectType.Keybind) => new()
        {
            Id = id,
            Name = name,
            Description = description,
            Type = type,
            MinimumLevel = level,
            Weight = 10,
            DurationSeconds = duration,
            CooldownSeconds = cooldown,
            CanStack = canStack
        };
}
