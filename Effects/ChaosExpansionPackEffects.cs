using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Effects;

// Twenty-seven lightweight effects built on the same public bases used by the
// original catalog. Each concrete class is parameterless, so ChaosEngine's
// reflection discovery picks it up without a registration list.
public abstract class ExpansionMacroBase : MacroChaosEffectBase
{
    private readonly ChaosEffectDefinition _definition;
    private readonly IReadOnlyList<InputMacroStep> _steps;

    protected ExpansionMacroBase(
        string id,
        string name,
        string description,
        IReadOnlyList<InputMacroStep> steps,
        ChaosLevel level = ChaosLevel.Normal,
        bool canStack = true)
    {
        _steps = steps;
        _definition = ExpansionDefinitions.Create(
            id, name, description, level, 4, 45, canStack);
    }

    public override ChaosEffectDefinition Definition => _definition;
    protected override IReadOnlyList<InputMacroStep> Steps => _steps;
}

public sealed class PanicSlideEffect : ExpansionMacroBase
{
    public PanicSlideEffect() : base(
        "panic_slide", "Panic Slide", "Forces a short sprint into a slide.",
        [
            new MacroPressAction(["sprint", "hold_sprint", "toggle_sprint", "key_sprint"], 260, 0xA0),
            new MacroDelay(120),
            new MacroPressAction(["crouch", "toggle_crouch", "hold_crouch", "key_crouch"], 160, 0xA2)
        ]) { }
}

public sealed class JumpReloadEffect : ExpansionMacroBase
{
    public JumpReloadEffect() : base(
        "jump_reload", "Aerial Reload", "Jumps and immediately reloads in mid-air.",
        [
            new MacroPressAction(["jump", "key_jump", "move_jump"], 110, 0x20),
            new MacroDelay(180),
            new MacroPressAction(["reload", "weapon_reload", "key_reload"], 110, 0x52)
        ]) { }
}

public sealed class GrenadeRetreatEffect : ExpansionMacroBase
{
    public GrenadeRetreatEffect() : base(
        "grenade_retreat", "Grenade Retreat", "Throws a grenade while backing away.",
        [
            new MacroHoldTogether([
                AbilityBindings.GrenadeAliases,
                ["move_backward", "move_back", "backward", "key_move_backward"]
            ], 420)
        ], ChaosLevel.Chaos) { }
}

public sealed class HeroLandingEffect : ExpansionMacroBase
{
    public HeroLandingEffect() : base(
        "hero_landing", "Hero Landing", "Jumps, looks down, then uses the class ability.",
        [
            new MacroPressAction(["jump", "key_jump", "move_jump"], 100, 0x20),
            new MacroDelay(220),
            new MacroMouseMove(0, 520, 150),
            new MacroPressAction(AbilityBindings.ClassAbilityAliases, 140, 0x56)
        ], ChaosLevel.Chaos, false) { }
}

public sealed class AirDashPanicEffect : ExpansionMacroBase
{
    public AirDashPanicEffect() : base(
        "air_dash_panic", "Air Dash Panic", "Jumps and triggers the configured air move.",
        [
            new MacroPressAction(["jump", "key_jump", "move_jump"], 100, 0x20),
            new MacroDelay(240),
            new MacroPressAction(AbilityBindings.AirMoveAliases, 120, 0x58)
        ]) { }
}

public sealed class WeaponCarouselEffect : ExpansionMacroBase
{
    public WeaponCarouselEffect() : base(
        "weapon_carousel", "Weapon Carousel", "Rapidly visits primary, special, and heavy weapons.",
        [
            new MacroPressAction(["weapon_1", "select_primary_weapon", "primary_weapon"], 80, 0x31),
            new MacroDelay(180),
            new MacroPressAction(["weapon_2", "select_special_weapon", "special_weapon"], 80, 0x32),
            new MacroDelay(180),
            new MacroPressAction(["weapon_3", "select_heavy_weapon", "heavy_weapon"], 80, 0x33)
        ]) { }
}

public sealed class MeleeComboEffect : ExpansionMacroBase
{
    public MeleeComboEffect() : base(
        "melee_combo", "Three Punch Combo", "Throws three measured melee attacks.",
        [
            new MacroPressAction(AbilityBindings.ChargedMeleeAliases, 90, 0x43),
            new MacroDelay(260),
            new MacroPressAction(AbilityBindings.ChargedMeleeAliases, 90, 0x43),
            new MacroDelay(260),
            new MacroPressAction(AbilityBindings.ChargedMeleeAliases, 90, 0x43)
        ]) { }
}

public sealed class LeapFireEffect : ExpansionMacroBase
{
    public LeapFireEffect() : base(
        "leap_fire", "Leap Fire", "Jumps and fires a single shot at the apex.",
        [
            new MacroPressAction(["jump", "key_jump", "move_jump"], 100, 0x20),
            new MacroDelay(300),
            new MacroPressMouseButton(MouseInputButton.Left, 90)
        ]) { }
}

public sealed class TacticalCrouchReloadEffect : ExpansionMacroBase
{
    public TacticalCrouchReloadEffect() : base(
        "crouch_reload", "Tactical Reload", "Crouches and reloads at the same time.",
        [
            new MacroHoldTogether([
                ["crouch", "toggle_crouch", "hold_crouch", "key_crouch"],
                ["reload", "weapon_reload", "key_reload"]
            ], 350)
        ]) { }
}

public sealed class PrimaryBurstEffect : ExpansionMacroBase
{
    public PrimaryBurstEffect() : base(
        "primary_burst", "Primary Burst", "Selects the primary weapon and fires a short burst.",
        [
            new MacroPressAction(["weapon_1", "select_primary_weapon", "primary_weapon"], 90, 0x31),
            new MacroDelay(220),
            new MacroPressMouseButton(MouseInputButton.Left, 220)
        ]) { }
}

public sealed class SpecialSnapEffect : ExpansionMacroBase
{
    public SpecialSnapEffect() : base(
        "special_snap", "Special Snap", "Equips the special weapon and snaps the view sideways.",
        [
            new MacroPressAction(["weapon_2", "select_special_weapon", "special_weapon"], 90, 0x32),
            new MacroDelay(160),
            new MacroMouseMove(460, 0, 130)
        ], ChaosLevel.Chaos) { }
}

public sealed class GrenadeMeleeComboEffect : ExpansionMacroBase
{
    public GrenadeMeleeComboEffect() : base(
        "grenade_melee_combo", "Space Magic Combo", "Throws a grenade then follows with a melee.",
        [
            new MacroPressAction(AbilityBindings.GrenadeAliases, 100, 0x51),
            new MacroDelay(320),
            new MacroPressAction(AbilityBindings.ChargedMeleeAliases, 110, 0x43)
        ], ChaosLevel.Chaos) { }
}

public abstract class ExpansionRepeatBase : RepeatingKeyChaosEffectBase
{
    private readonly ChaosEffectDefinition _definition;
    private readonly ushort _key;
    private readonly int _count;
    private readonly int _delay;

    protected ExpansionRepeatBase(
        string id, string name, string description,
        ushort key, int count, int delay)
    {
        _key = key;
        _count = count;
        _delay = delay;
        _definition = ExpansionDefinitions.Create(
            id, name, description, ChaosLevel.Normal, 5, 50, true);
    }

    public override ChaosEffectDefinition Definition => _definition;
    protected override ushort VirtualKey => _key;
    protected override int PressCount(ChaosLevel level) => _count;
    protected override TimeSpan DelayBetweenPresses => TimeSpan.FromMilliseconds(_delay);
}

public sealed class ReloadHiccupsEffect : ExpansionRepeatBase
{
    public ReloadHiccupsEffect() : base(
        "reload_hiccups", "Reload Hiccups", "Taps reload several times with a steady delay.",
        0x52, 5, 420) { }
}

public sealed class JumpingBeansEffect : ExpansionRepeatBase
{
    public JumpingBeansEffect() : base(
        "jumping_beans", "Jumping Beans", "Taps jump four times.",
        0x20, 4, 520) { }
}

public sealed class CrouchBeatEffect : ExpansionRepeatBase
{
    public CrouchBeatEffect() : base(
        "crouch_beat", "Crouch Beat", "Taps the default crouch key in rhythm.",
        0xA2, 6, 310) { }
}

public sealed class PrimaryRecallEffect : ExpansionRepeatBase
{
    public PrimaryRecallEffect() : base(
        "primary_recall", "Primary Recall", "Repeatedly calls the primary weapon slot.",
        0x31, 6, 430) { }
}

public sealed class GhostFlickerEffect : ExpansionRepeatBase
{
    public GhostFlickerEffect() : base(
        "ghost_flicker", "Ghost Flicker", "Briefly flickers the default Ghost key.",
        0x09, 4, 360) { }
}

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

public sealed class DisableWeaponSlotsEffect : ExpansionDisableBase
{
    public DisableWeaponSlotsEffect() : base(
        "disable_weapon_slots", "Weapon Selector Offline",
        "Temporarily blocks all detected direct weapon-slot keys.",
        ["weapon_1", "weapon_2", "weapon_3", "select_primary_weapon",
         "select_special_weapon", "select_heavy_weapon"],
        [Key(0x31, "primary"), Key(0x32, "special"), Key(0x33, "heavy")]) { }

    private static InputBinding Key(ushort key, string name) =>
        CommonInputFallbacks.Keyboard(key, $"default {name} weapon");
}

public sealed class DisableFinisherEffect : ExpansionDisableBase
{
    public DisableFinisherEffect() : base(
        "disable_finisher", "No Finishers", "Disables finishers and the default G key.",
        ["finisher", "perform_finisher", "key_finisher"],
        [CommonInputFallbacks.Keyboard(0x47, "default finisher")]) { }
}

public sealed class DisableGhostEffect : ExpansionDisableBase
{
    public DisableGhostEffect() : base(
        "disable_ghost", "Ghost Radio Silence", "Disables Ghost and the default Tab key.",
        ["ghost", "open_ghost", "nav_mode", "key_ghost"],
        [CommonInputFallbacks.Keyboard(0x09, "default ghost")]) { }
}

public sealed class DisableEmotesEffect : ExpansionDisableBase
{
    public DisableEmotesEffect() : base(
        "disable_emotes", "Serious Business", "Disables detected emotes and all arrow keys.",
        ["emote_1", "emote_2", "emote_3", "emote_4"],
        [
            CommonInputFallbacks.Keyboard(0x25, "left emote"),
            CommonInputFallbacks.Keyboard(0x26, "up emote"),
            CommonInputFallbacks.Keyboard(0x27, "right emote"),
            CommonInputFallbacks.Keyboard(0x28, "down emote")
        ]) { }
}

public sealed class DisableInventoryEffect : ExpansionDisableBase
{
    public DisableInventoryEffect() : base(
        "disable_inventory", "Inventory Locked", "Disables inventory and the default I key.",
        ["inventory", "open_inventory", "character_screen", "key_inventory"],
        [CommonInputFallbacks.Keyboard(0x49, "default inventory")]) { }
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
