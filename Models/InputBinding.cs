namespace CryoChaos.Models;

public enum InputBindingKind
{
    Unknown,
    Keyboard,
    MouseButton,
    MouseWheel
}

public enum MouseInputButton
{
    None,
    Left,
    Right,
    Middle,
    XButton1,
    XButton2
}

public enum MouseWheelDirection
{
    None,
    Up,
    Down
}

public sealed class InputBinding
{
    private string _rawValue = string.Empty;

    public string RawValue
    {
        get => _rawValue;
        init => _rawValue = value ?? string.Empty;
    }

    // Compatibility name for older project code.
    public string RawName
    {
        get => _rawValue;
        init => _rawValue = value ?? string.Empty;
    }

    public InputBindingKind Kind { get; init; }
    public ushort VirtualKey { get; init; }
    public MouseInputButton MouseButton { get; init; }
    public MouseWheelDirection WheelDirection { get; init; }

    public override string ToString() => Kind switch
    {
        InputBindingKind.Keyboard =>
            $"{RawValue} (Keyboard 0x{VirtualKey:X2})",

        InputBindingKind.MouseButton =>
            $"{RawValue} ({MouseButton})",

        InputBindingKind.MouseWheel =>
            $"{RawValue} ({WheelDirection})",

        _ => $"{RawValue} (Unknown)"
    };
}
