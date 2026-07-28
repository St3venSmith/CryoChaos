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
        "disable_inventory", "Menu Lock",
        "Disables detected character, inventory, map, director, quest, roster, and pause-menu bindings.",
        [
            "inventory",
            "open_inventory",
            "ui_open_inventory",
            "character_screen",
            "open_character",
            "ui_open_character",
            "key_inventory",
            "map",
            "open_map",
            "ui_open_map",
            "director",
            "open_director",
            "ui_open_director",
            "quests",
            "quest_log",
            "open_quests",
            "ui_open_quests",
            "roster",
            "open_roster",
            "ui_open_roster",
            "clan",
            "open_clan",
            "ui_open_clan",
            "season",
            "open_season",
            "ui_open_season",
            "store",
            "open_store",
            "ui_open_store",
            "journey",
            "open_journey",
            "ui_open_journey",
            "loadouts",
            "open_loadouts",
            "ui_open_loadouts",
            "ghost",
            "open_ghost",
            "nav_mode",
            "game_menu",
            "open_game_menu",
            "pause",
            "menu",
            "ui_open_settings"
        ],
        []) { }

    // Destiny can place one alternate menu binding on a mouse button. Raw
    // mouse buttons cannot be reliably suppressed externally, but that must
    // not prevent all detected keyboard menu bindings from being locked.
    protected override bool IgnoreUnsupportedMouseBindings => true;
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
        bool canStack) => new()
        {
            Id = id,
            Name = name,
            Description = description,
            Type = ChaosEffectType.Keybind,
            MinimumLevel = level,
            Weight = 10,
            DurationSeconds = duration,
            CooldownSeconds = cooldown,
            CanStack = canStack
        };
}
