using CryoChaos.Models;

namespace CryoChaos.Services;

public static class InputBindingParser
{
    private static readonly Dictionary<string, ushort> NamedKeys =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["space"] = 0x20,
            ["spacebar"] = 0x20,
            ["escape"] = 0x1B,
            ["esc"] = 0x1B,
            ["enter"] = 0x0D,
            ["return"] = 0x0D,
            ["tab"] = 0x09,
            ["backspace"] = 0x08,

            ["shift"] = 0xA0,
            ["leftshift"] = 0xA0,
            ["lshift"] = 0xA0,
            ["rightshift"] = 0xA1,
            ["rshift"] = 0xA1,

            ["ctrl"] = 0xA2,
            ["control"] = 0xA2,
            ["leftctrl"] = 0xA2,
            ["leftcontrol"] = 0xA2,
            ["lctrl"] = 0xA2,
            ["rightctrl"] = 0xA3,
            ["rightcontrol"] = 0xA3,
            ["rctrl"] = 0xA3,

            ["alt"] = 0xA4,
            ["leftalt"] = 0xA4,
            ["lalt"] = 0xA4,
            ["rightalt"] = 0xA5,
            ["ralt"] = 0xA5,

            ["up"] = 0x26,
            ["uparrow"] = 0x26,
            ["down"] = 0x28,
            ["downarrow"] = 0x28,
            ["left"] = 0x25,
            ["leftarrow"] = 0x25,
            ["right"] = 0x27,
            ["rightarrow"] = 0x27,

            ["capslock"] = 0x14,
            ["delete"] = 0x2E,
            ["del"] = 0x2E,
            ["insert"] = 0x2D,
            ["ins"] = 0x2D,
            ["home"] = 0x24,
            ["end"] = 0x23,
            ["pageup"] = 0x21,
            ["pagedown"] = 0x22,

            ["grave"] = 0xC0,
            ["tilde"] = 0xC0,
            ["backtick"] = 0xC0,
            ["semicolon"] = 0xBA,
            ["equals"] = 0xBB,
            ["comma"] = 0xBC,
            ["minus"] = 0xBD,
            ["period"] = 0xBE,
            ["slash"] = 0xBF,
            ["leftbracket"] = 0xDB,
            ["backslash"] = 0xDC,
            ["rightbracket"] = 0xDD,
            ["apostrophe"] = 0xDE
        };

    public static InputBinding? Parse(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        string normalized = Normalize(rawValue);

        if (normalized is "unused" or "none" or "unbound")
        {
            return null;
        }

        if (TryParseMouseButton(
                rawValue,
                normalized,
                out InputBinding? mouseButton))
        {
            return mouseButton;
        }

        if (TryParseMouseWheel(
                rawValue,
                normalized,
                out InputBinding? mouseWheel))
        {
            return mouseWheel;
        }

        if (NamedKeys.TryGetValue(
                normalized,
                out ushort namedVirtualKey))
        {
            return CreateKeyboard(rawValue, namedVirtualKey);
        }

        if (normalized.Length == 1)
        {
            char character = normalized[0];

            if (character is >= 'a' and <= 'z')
            {
                return CreateKeyboard(
                    rawValue,
                    (ushort)char.ToUpperInvariant(character));
            }

            if (character is >= '0' and <= '9')
            {
                return CreateKeyboard(rawValue, character);
            }
        }

        if (normalized.StartsWith('f') &&
            normalized.Length > 1 &&
            int.TryParse(normalized[1..], out int functionNumber) &&
            functionNumber is >= 1 and <= 24)
        {
            return CreateKeyboard(
                rawValue,
                (ushort)(0x70 + functionNumber - 1));
        }

        if (TryParseNumpad(normalized, out ushort numpadVirtualKey))
        {
            return CreateKeyboard(rawValue, numpadVirtualKey);
        }

        return new InputBinding
        {
            RawValue = rawValue,
            Kind = InputBindingKind.Unknown
        };
    }

    private static bool TryParseMouseButton(
        string rawValue,
        string normalized,
        out InputBinding? binding)
    {
        MouseInputButton button = normalized switch
        {
            "mouse1" or
            "mousebutton1" or
            "leftmouse" or
            "leftmousebutton" or
            "mouseleft" =>
                MouseInputButton.Left,

            "mouse2" or
            "mousebutton2" or
            "rightmouse" or
            "rightmousebutton" or
            "mouseright" =>
                MouseInputButton.Right,

            "mouse3" or
            "mousebutton3" or
            "middlemouse" or
            "middlemousebutton" or
            "mousemiddle" =>
                MouseInputButton.Middle,

            // Destiny writes side buttons in this form.
            "extramousebutton1" or
            "mouse4" or
            "mousebutton4" or
            "xbutton1" or
            "mousexbutton1" or
            "mousex1" or
            "thumbbutton1" or
            "thumbmousebutton1" or
            "sidebutton1" or
            "backmousebutton" =>
                MouseInputButton.XButton1,

            "extramousebutton2" or
            "mouse5" or
            "mousebutton5" or
            "xbutton2" or
            "mousexbutton2" or
            "mousex2" or
            "thumbbutton2" or
            "thumbmousebutton2" or
            "sidebutton2" or
            "forwardmousebutton" =>
                MouseInputButton.XButton2,

            _ => MouseInputButton.None
        };

        if (button == MouseInputButton.None)
        {
            binding = null;
            return false;
        }

        binding = new InputBinding
        {
            RawValue = rawValue,
            Kind = InputBindingKind.MouseButton,
            MouseButton = button
        };

        return true;
    }

    private static bool TryParseMouseWheel(
        string rawValue,
        string normalized,
        out InputBinding? binding)
    {
        MouseWheelDirection direction = normalized switch
        {
            "mousewheelup" or
            "wheelup" or
            "mwheelup" or
            "scrollup" =>
                MouseWheelDirection.Up,

            "mousewheeldown" or
            "wheeldown" or
            "mwheeldown" or
            "scrolldown" =>
                MouseWheelDirection.Down,

            _ => MouseWheelDirection.None
        };

        if (direction == MouseWheelDirection.None)
        {
            binding = null;
            return false;
        }

        binding = new InputBinding
        {
            RawValue = rawValue,
            Kind = InputBindingKind.MouseWheel,
            WheelDirection = direction
        };

        return true;
    }

    private static bool TryParseNumpad(
        string normalized,
        out ushort virtualKey)
    {
        string? numberPart = null;

        if (normalized.StartsWith("numpad", StringComparison.Ordinal))
        {
            numberPart = normalized[6..];
        }
        else if (normalized.StartsWith("keypad", StringComparison.Ordinal))
        {
            numberPart = normalized[6..];
        }

        if (numberPart is not null &&
            int.TryParse(numberPart, out int number) &&
            number is >= 0 and <= 9)
        {
            virtualKey = (ushort)(0x60 + number);
            return true;
        }

        virtualKey = 0;
        return false;
    }

    private static InputBinding CreateKeyboard(
        string rawValue,
        ushort virtualKey)
    {
        return new InputBinding
        {
            RawValue = rawValue,
            Kind = InputBindingKind.Keyboard,
            VirtualKey = virtualKey
        };
    }

    private static string Normalize(string value)
    {
        return new string(
            value
                .Trim()
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
    }
}
