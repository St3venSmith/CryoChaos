using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Effects;

internal static class CommonInputFallbacks
{
    public static InputBinding Keyboard(ushort virtualKey, string name) =>
        new()
        {
            RawValue = name,
            Kind = InputBindingKind.Keyboard,
            VirtualKey = virtualKey
        };

    public static InputBinding Mouse(MouseInputButton button, string name) =>
        new()
        {
            RawValue = name,
            Kind = InputBindingKind.MouseButton,
            MouseButton = button
        };
}

public sealed class DisableJumpEffect : InputDisableChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "disable_jump",
        Name = "Boots Glued Down",
        Description = "Disables the detected jump binding and default Space key.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 7,
        DurationSeconds = 14,
        CooldownSeconds = 50,
        CanStack = true
    };

    protected override string[] ActionAliases =>
        ["jump", "key_jump", "move_jump"];

    protected override IReadOnlyList<InputBinding> AdditionalBindings =>
        [CommonInputFallbacks.Keyboard(0x20, "default jump")];
}

public sealed class DisableSprintEffect : InputDisableChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "disable_sprint",
        Name = "Out Of Breath",
        Description = "Disables sprint, including both Shift keys.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 7,
        DurationSeconds = 14,
        CooldownSeconds = 50,
        CanStack = true
    };

    protected override string[] ActionAliases =>
        ["sprint", "hold_sprint", "toggle_sprint", "key_sprint"];

    protected override IReadOnlyList<InputBinding> AdditionalBindings =>
    [
        CommonInputFallbacks.Keyboard(0xA0, "default left shift"),
        CommonInputFallbacks.Keyboard(0xA1, "default right shift")
    ];
}

public sealed class DisableCrouchEffect : InputDisableChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "disable_crouch",
        Name = "No Sliding",
        Description = "Disables crouching and sliding, including both Ctrl keys.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 7,
        DurationSeconds = 14,
        CooldownSeconds = 50,
        CanStack = true
    };

    protected override string[] ActionAliases =>
        ["crouch", "toggle_crouch", "hold_crouch", "key_crouch"];

    protected override IReadOnlyList<InputBinding> AdditionalBindings =>
    [
        CommonInputFallbacks.Keyboard(0xA2, "default left ctrl"),
        CommonInputFallbacks.Keyboard(0xA3, "default right ctrl")
    ];
}

public sealed class DisableReloadEffect : InputDisableChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "disable_reload",
        Name = "Magazine Stuck",
        Description = "Disables reload, including the default R key.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 7,
        DurationSeconds = 14,
        CooldownSeconds = 50,
        CanStack = true
    };

    protected override string[] ActionAliases =>
        ["reload", "weapon_reload", "key_reload"];

    protected override IReadOnlyList<InputBinding> AdditionalBindings =>
        [CommonInputFallbacks.Keyboard(0x52, "default reload")];
}

public sealed class DisableInteractEffect : InputDisableChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "disable_interact",
        Name = "Do Not Touch",
        Description = "Disables interact and revive, including the default E key.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 5,
        DurationSeconds = 14,
        CooldownSeconds = 55,
        CanStack = true
    };

    protected override string[] ActionAliases =>
        ["interact", "use", "revive", "key_interact"];

    protected override IReadOnlyList<InputBinding> AdditionalBindings =>
        [CommonInputFallbacks.Keyboard(0x45, "default interact")];
}

public sealed class DisableSuperEffect : InputDisableChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "disable_super",
        Name = "Super Drained",
        Description = "Disables the Super ability, including the default F key.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 5,
        DurationSeconds = 14,
        CooldownSeconds = 55,
        CanStack = true
    };

    protected override string[] ActionAliases =>
        ["super", "use_super", "activate_super", "key_super"];

    protected override IReadOnlyList<InputBinding> AdditionalBindings =>
        [CommonInputFallbacks.Keyboard(0x46, "default super")];
}

public sealed class DisableAimEffect : InputDisableChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "disable_aim",
        Name = "Hip Fire Only",
        Description = "Disables aim-down-sights, including default right click.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 6,
        DurationSeconds = 14,
        CooldownSeconds = 55,
        CanStack = true
    };

    protected override string[] ActionAliases =>
        ["aim", "ads", "zoom", "secondary_fire", "key_aim"];

    protected override IReadOnlyList<InputBinding> AdditionalBindings =>
        [CommonInputFallbacks.Mouse(MouseInputButton.Right, "default aim")];
}

public sealed class ForcedReloadEffect : MacroChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "forced_reload",
        Name = "Bad Time To Reload",
        Description = "Immediately presses the detected reload binding.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Low,
        Weight = 10,
        DurationSeconds = 2,
        CooldownSeconds = 35,
        CanStack = true
    };

    protected override IReadOnlyList<InputMacroStep> Steps { get; } =
    [
        new MacroPressAction(
            ["reload", "weapon_reload", "key_reload"],
            HoldMilliseconds: 100,
            FallbackVirtualKey: 0x52)
    ];
}

public sealed class AccidentalSuperEffect : MacroChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "accidental_super",
        Name = "Accidental Super",
        Description = "Immediately presses the detected Super binding.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 3,
        DurationSeconds = 2,
        CooldownSeconds = 55,
        CanStack = true
    };

    protected override IReadOnlyList<InputMacroStep> Steps { get; } =
    [
        new MacroPressAction(
            ["super", "use_super", "activate_super", "key_super"],
            HoldMilliseconds: 120,
            FallbackVirtualKey: 0x46)
    ];
}

public sealed class AccidentalGrenadeEffect : MacroChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "accidental_grenade",
        Name = "Butterfingers Grenade",
        Description = "Immediately presses the detected grenade binding.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 7,
        DurationSeconds = 2,
        CooldownSeconds = 35,
        CanStack = true
    };

    protected override IReadOnlyList<InputMacroStep> Steps { get; } =
    [
        new MacroPressAction(
            AbilityBindings.GrenadeAliases,
            HoldMilliseconds: 110,
            FallbackVirtualKey: 0x51)
    ];
}

public sealed class AccidentalMeleeEffect : MacroChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "accidental_melee",
        Name = "Shadow Boxing",
        Description = "Immediately presses the detected charged-melee binding.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 7,
        DurationSeconds = 2,
        CooldownSeconds = 35,
        CanStack = true
    };

    protected override IReadOnlyList<InputMacroStep> Steps { get; } =
    [
        new MacroPressAction(
            AbilityBindings.ChargedMeleeAliases,
            HoldMilliseconds: 110,
            FallbackVirtualKey: 0x43)
    ];
}

public sealed class AccidentalClassAbilityEffect : MacroChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "accidental_class_ability",
        Name = "Misplaced Barricade",
        Description = "Immediately presses the detected class-ability binding.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 7,
        DurationSeconds = 2,
        CooldownSeconds = 35,
        CanStack = true
    };

    protected override IReadOnlyList<InputMacroStep> Steps { get; } =
    [
        new MacroPressAction(
            AbilityBindings.ClassAbilityAliases,
            HoldMilliseconds: 140,
            FallbackVirtualKey: 0x56)
    ];
}

public sealed class AirborneArsenalEffect : MacroChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "airborne_arsenal",
        Name = "Airborne Arsenal",
        Description = "Jumps, throws a grenade, equips heavy, and fires in the air.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 3,
        DurationSeconds = 5,
        CooldownSeconds = 60,
        CanStack = false
    };

    protected override IReadOnlyList<InputMacroStep> Steps { get; } =
    [
        new MacroPressAction(
            ["jump", "key_jump", "move_jump"],
            HoldMilliseconds: 120,
            FallbackVirtualKey: 0x20),
        new MacroDelay(300),
        new MacroPressAction(
            AbilityBindings.GrenadeAliases,
            HoldMilliseconds: 110,
            FallbackVirtualKey: 0x51),
        new MacroDelay(180),
        new MacroPressAction(
            ["weapon_3", "select_heavy_weapon", "heavy_weapon", "switch_to_heavy_weapon"],
            HoldMilliseconds: 110,
            FallbackVirtualKey: 0x33),
        new MacroDelay(230),
        new MacroPressMouseButton(MouseInputButton.Left, 140)
    ];
}
