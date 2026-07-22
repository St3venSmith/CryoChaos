using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Effects;

internal static class AbilityBindings
{
    public static readonly string[] GrenadeAliases =
        ["grenade", "throw_grenade", "use_grenade", "key_grenade"];

    public static readonly string[] ClassAbilityAliases =
    [
        "class_ability",
        "classability",
        "use_class_ability",
        "key_class_ability"
    ];

    public static readonly string[] ChargedMeleeAliases =
    [
        "melee",
        "melee2",
        "melee_attack",
        "melee_ability",
        "charged_melee",
        "chargedmelee",
        "melee_charged",
        "key_charged_melee",
        "auto_melee",
        "automelee",
        "automatic_melee",
        "melee_auto",
        "key_auto_melee",
        "uncharged_melee",
        "unchargedmelee",
        "melee_uncharged",
        "key_uncharged_melee",
        "key_melee"
    ];

    public static readonly string[] AirMoveAliases =
    [
        "air_move",
        "airmove",
        "use_air_move",
        "key_air_move",
        "icarus_dash",
        "shatterdive",
        "phoenix_dive"
    ];

    public static IReadOnlyList<InputBinding> ResolveAll(
        ChaosEffectContext context)
    {
        List<InputBinding> bindings = [];
        bindings.AddRange(
            context.Keybinds.ResolveActionBindings(GrenadeAliases));
        bindings.AddRange(
            context.Keybinds.ResolveActionBindings(ClassAbilityAliases));
        bindings.AddRange(
            context.Keybinds.ResolveActionBindings(ChargedMeleeAliases));
        bindings.AddRange(
            context.Keybinds.ResolveActionBindings(AirMoveAliases));

        bindings.Add(CreateFallback(0x51, "default grenade"));       // Q
        bindings.Add(CreateFallback(0x56, "default class ability")); // V
        bindings.Add(CreateFallback(0x43, "default charged melee")); // C
        bindings.Add(CreateFallback(0x58, "default air move"));      // X

        return bindings
            .DistinctBy(binding =>
                (binding.Kind,
                 binding.VirtualKey,
                 binding.MouseButton,
                 binding.WheelDirection))
            .ToArray();
    }

    public static InputBinding CreateFallback(
        ushort virtualKey,
        string name) =>
        new()
        {
            RawValue = name,
            Kind = InputBindingKind.Keyboard,
            VirtualKey = virtualKey
        };
}

public sealed class DisableGrenadeEffect : InputDisableChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "disable_grenade",
        Name = "Grenade Safety On",
        Description = "Disables both detected grenade bindings and the default Q key.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 7,
        DurationSeconds = 14,
        CooldownSeconds = 50,
        CanStack = true
    };

    protected override string[] ActionAliases =>
        AbilityBindings.GrenadeAliases;

    protected override IReadOnlyList<InputBinding> AdditionalBindings =>
        [AbilityBindings.CreateFallback(0x51, "default grenade")];
}

public sealed class DisableClassAbilityEffect : InputDisableChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "disable_class_ability",
        Name = "Class Ability Offline",
        Description = "Disables both detected class-ability bindings and the default V key.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 7,
        DurationSeconds = 14,
        CooldownSeconds = 50,
        CanStack = true
    };

    protected override string[] ActionAliases =>
        AbilityBindings.ClassAbilityAliases;

    protected override IReadOnlyList<InputBinding> AdditionalBindings =>
        [AbilityBindings.CreateFallback(0x56, "default class ability")];
}

public sealed class DisableChargedMeleeEffect : InputDisableChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "disable_charged_melee",
        Name = "Melee Battery Empty",
        Description = "Disables charged, auto, and uncharged melee bindings plus default C.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 7,
        DurationSeconds = 14,
        CooldownSeconds = 50,
        CanStack = true
    };

    protected override string[] ActionAliases =>
        AbilityBindings.ChargedMeleeAliases;

    protected override IReadOnlyList<InputBinding> AdditionalBindings =>
        [AbilityBindings.CreateFallback(0x43, "default charged melee")];
}

public sealed class DisableAirMoveEffect : IChaosEffect
{
    public ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "disable_air_move",
        Name = "Grounded",
        Description = "Disables air moves and jumping, including default X and Space.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 6,
        DurationSeconds = 14,
        CooldownSeconds = 50,
        CanStack = true
    };

    public async Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        if (!await MovementBindings.EnsureDestinyForegroundAsync(
                cancellationToken))
        {
            return;
        }

        InputBinding[] bindings = context.Keybinds
            .ResolveActionBindings(AbilityBindings.AirMoveAliases)
            .Concat(context.Keybinds.ResolveActionBindings(
                "jump", "key_jump", "move_jump"))
            .Append(AbilityBindings.CreateFallback(0x58, "default air move"))
            .Append(AbilityBindings.CreateFallback(0x20, "default jump"))
            .DistinctBy(binding =>
                (binding.Kind,
                 binding.VirtualKey,
                 binding.MouseButton,
                 binding.WheelDirection))
            .ToArray();

        await context.InputRemapper.SuppressAsync(
            bindings,
            context.GetEffectDuration(Definition),
            cancellationToken);
    }
}

public sealed class DisableAllAbilitiesEffect : IChaosEffect
{
    public ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "disable_all_abilities",
        Name = "Ability Blackout",
        Description = "Disables grenade, class ability, charged melee, and air move.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 4,
        DurationSeconds = 16,
        CooldownSeconds = 70,
        CanStack = false
    };

    public async Task RunAsync(
        ChaosEffectContext context,
        CancellationToken cancellationToken)
    {
        if (!await MovementBindings.EnsureDestinyForegroundAsync(
                cancellationToken))
        {
            return;
        }

        await context.InputRemapper.SuppressAsync(
            AbilityBindings.ResolveAll(context),
            context.GetEffectDuration(Definition),
            cancellationToken);
    }
}

public sealed class AbilityDumpMacroEffect : MacroChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "ability_dump_macro",
        Name = "Empty The Tank",
        Description = "Uses grenade, charged melee, then class ability in sequence.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 5,
        DurationSeconds = 5,
        CooldownSeconds = 55,
        CanStack = false
    };

    protected override IReadOnlyList<InputMacroStep> Steps { get; } =
    [
        new MacroPressAction(
            AbilityBindings.GrenadeAliases,
            HoldMilliseconds: 110,
            FallbackVirtualKey: 0x51),
        new MacroDelay(280),
        new MacroPressAction(
            AbilityBindings.ChargedMeleeAliases,
            HoldMilliseconds: 110,
            FallbackVirtualKey: 0x43),
        new MacroDelay(320),
        new MacroPressAction(
            AbilityBindings.ClassAbilityAliases,
            HoldMilliseconds: 140,
            FallbackVirtualKey: 0x56)
    ];
}

public sealed class AerialDisasterMacroEffect : MacroChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "aerial_disaster_macro",
        Name = "Aerial Disaster",
        Description = "Jumps, triggers the air move, then throws a grenade.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 5,
        DurationSeconds = 5,
        CooldownSeconds = 50,
        CanStack = false
    };

    protected override IReadOnlyList<InputMacroStep> Steps { get; } =
    [
        new MacroPressAction(
            ["jump", "key_jump", "move_jump"],
            HoldMilliseconds: 120,
            FallbackVirtualKey: 0x20),
        new MacroDelay(380),
        new MacroPressAction(
            AbilityBindings.AirMoveAliases,
            HoldMilliseconds: 120,
            FallbackVirtualKey: 0x58),
        new MacroDelay(260),
        new MacroPressAction(
            AbilityBindings.GrenadeAliases,
            HoldMilliseconds: 110,
            FallbackVirtualKey: 0x51)
    ];
}

public sealed class BadRotationMacroEffect : MacroChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "bad_rotation_macro",
        Name = "Bad Ability Rotation",
        Description = "Uses class ability, grenade, air move, and charged melee rapidly.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 4,
        DurationSeconds = 6,
        CooldownSeconds = 60,
        CanStack = false
    };

    protected override IReadOnlyList<InputMacroStep> Steps { get; } =
    [
        new MacroPressAction(
            AbilityBindings.ClassAbilityAliases,
            HoldMilliseconds: 130,
            FallbackVirtualKey: 0x56),
        new MacroDelay(240),
        new MacroPressAction(
            AbilityBindings.GrenadeAliases,
            HoldMilliseconds: 110,
            FallbackVirtualKey: 0x51),
        new MacroDelay(240),
        new MacroPressAction(
            AbilityBindings.AirMoveAliases,
            HoldMilliseconds: 110,
            FallbackVirtualKey: 0x58),
        new MacroDelay(240),
        new MacroPressAction(
            AbilityBindings.ChargedMeleeAliases,
            HoldMilliseconds: 120,
            FallbackVirtualKey: 0x43)
    ];
}
