using CryoChaos.Models;

namespace CryoChaos.Effects;

public abstract class RepeatedDetectedActionEffectBase :
    KeybindChaosEffectBase
{
    private readonly ChaosEffectDefinition _definition;
    private readonly string[] _aliases;
    private readonly int _repeatCount;
    private readonly int _repeatDelayMilliseconds;

    protected RepeatedDetectedActionEffectBase(
        string id,
        string name,
        string description,
        string[] aliases,
        ChaosLevel minimumLevel,
        int repeatCount,
        int repeatDelayMilliseconds)
    {
        _aliases = aliases;
        _repeatCount = repeatCount;
        _repeatDelayMilliseconds = repeatDelayMilliseconds;
        _definition = new ChaosEffectDefinition
        {
            Id = id,
            Name = name,
            Description = description,
            Type = ChaosEffectType.Keybind,
            MinimumLevel = minimumLevel,
            Weight = 100,
            DurationSeconds = Math.Max(
                2,
                (repeatCount * repeatDelayMilliseconds) / 1000 + 1),
            CooldownSeconds = 45,
            CanStack = true
        };
    }

    public override ChaosEffectDefinition Definition => _definition;
    protected override string[] ActionAliases => _aliases;
    protected override int RepeatCount(ChaosLevel level) =>
        _repeatCount + (level == ChaosLevel.Chaos ? 2 : 0);
    protected override TimeSpan RepeatDelay =>
        TimeSpan.FromMilliseconds(_repeatDelayMilliseconds);
}

public sealed class ReloadHiccupsEffect :
    RepeatedDetectedActionEffectBase
{
    public ReloadHiccupsEffect() : base(
        "reload_hiccups",
        "Reload Hiccups",
        "Repeatedly taps the player's detected reload binding.",
        ["reload", "weapon_reload", "key_reload"],
        ChaosLevel.Normal,
        repeatCount: 6,
        repeatDelayMilliseconds: 430)
    {
    }
}

public sealed class CrouchStutterEffect :
    RepeatedDetectedActionEffectBase
{
    public CrouchStutterEffect() : base(
        "crouch_stutter",
        "Crouch Stutter",
        "Repeatedly taps the player's detected crouch binding.",
        ["crouch", "toggle_crouch", "hold_crouch", "key_crouch"],
        ChaosLevel.Normal,
        repeatCount: 7,
        repeatDelayMilliseconds: 360)
    {
    }
}

public sealed class WeaponFidgetEffect :
    RepeatedDetectedActionEffectBase
{
    public WeaponFidgetEffect() : base(
        "weapon_fidget",
        "Weapon Fidget",
        "Repeatedly taps the player's detected next-weapon binding.",
        ["next_weapon", "cycle_weapon", "weapon_cycle", "switch_weapon"],
        ChaosLevel.Normal,
        repeatCount: 8,
        repeatDelayMilliseconds: 310)
    {
    }
}

public sealed class AccidentalInteractSpamEffect :
    RepeatedDetectedActionEffectBase
{
    public AccidentalInteractSpamEffect() : base(
        "interact_spam",
        "Compulsive Interact",
        "Repeatedly taps the player's detected interact binding.",
        ["interact", "use", "revive", "key_interact"],
        ChaosLevel.Chaos,
        repeatCount: 6,
        repeatDelayMilliseconds: 500)
    {
    }
}

public sealed class AimZigzagEffect : MacroChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "aim_zigzag",
        Name = "Aim Zigzag",
        Description = "Jerks the camera left and right in an uneven pattern.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 100,
        DurationSeconds = 3,
        CooldownSeconds = 45,
        CanStack = true
    };

    protected override IReadOnlyList<InputMacroStep> Steps { get; } =
    [
        new MacroMouseMove(-260, -35, 100),
        new MacroDelay(90),
        new MacroMouseMove(420, 70, 130),
        new MacroDelay(80),
        new MacroMouseMove(-330, -55, 110),
        new MacroDelay(70),
        new MacroMouseMove(210, 20, 90)
    ];
}

public sealed class CameraStaircaseEffect : MacroChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "camera_staircase",
        Name = "Camera Staircase",
        Description = "Walks the camera through a distracting staircase pattern.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Chaos,
        Weight = 100,
        DurationSeconds = 4,
        CooldownSeconds = 50,
        CanStack = true
    };

    protected override IReadOnlyList<InputMacroStep> Steps { get; } =
    [
        new MacroMouseMove(190, 0, 100),
        new MacroMouseMove(0, 150, 100),
        new MacroMouseMove(-190, 0, 100),
        new MacroMouseMove(0, 150, 100),
        new MacroMouseMove(190, 0, 100),
        new MacroMouseMove(0, -300, 140)
    ];
}

public sealed class MovementCoinFlipEffect : MacroChaosEffectBase
{
    public override ChaosEffectDefinition Definition { get; } = new()
    {
        Id = "movement_coin_flip",
        Name = "Movement Coin Flip",
        Description = "Alternates the player's detected left and right movement bindings.",
        Type = ChaosEffectType.Keybind,
        MinimumLevel = ChaosLevel.Normal,
        Weight = 100,
        DurationSeconds = 4,
        CooldownSeconds = 45,
        CanStack = true
    };

    protected override IReadOnlyList<InputMacroStep> Steps { get; } =
    [
        new MacroPressAction(["move_left", "left", "strafe_left"], 280),
        new MacroDelay(90),
        new MacroPressAction(["move_right", "right", "strafe_right"], 380),
        new MacroDelay(90),
        new MacroPressAction(["move_left", "left", "strafe_left"], 460),
        new MacroDelay(80),
        new MacroPressAction(["move_right", "right", "strafe_right"], 240)
    ];
}
