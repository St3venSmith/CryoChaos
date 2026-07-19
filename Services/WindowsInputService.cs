using System.ComponentModel;
using System.Runtime.InteropServices;
using CryoChaos.Models;

namespace CryoChaos.Services;

public sealed class WindowsInputService
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;

    private const uint KeyEventKeyUp = 0x0002;

    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;

    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;

    private const uint MouseEventMiddleDown = 0x0020;
    private const uint MouseEventMiddleUp = 0x0040;

    private const uint MouseEventXDown = 0x0080;
    private const uint MouseEventXUp = 0x0100;

    private const uint MouseEventWheel = 0x0800;

    private const uint XButton1 = 0x0001;
    private const uint XButton2 = 0x0002;

    private const int WheelDelta = 120;

    public async Task PressAsync(
        InputBinding binding,
        TimeSpan holdDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);

        switch (binding.Kind)
        {
            case InputBindingKind.Keyboard:
                await PressKeyboardAsync(
                    binding.VirtualKey,
                    holdDuration,
                    cancellationToken);
                break;

            case InputBindingKind.MouseButton:
                await PressMouseButtonAsync(
                    binding.MouseButton,
                    holdDuration,
                    cancellationToken);
                break;

            case InputBindingKind.MouseWheel:
                ScrollMouseWheel(binding.WheelDirection);
                break;

            case InputBindingKind.Unknown:
            default:
                throw new InvalidOperationException(
                    $"Cannot simulate unknown binding '{binding.RawValue}'.");
        }
    }

    public async Task PressKeyboardAsync(
        ushort virtualKey,
        TimeSpan holdDuration,
        CancellationToken cancellationToken = default)
    {
        if (virtualKey == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(virtualKey),
                "Virtual key cannot be zero.");
        }

        SendKeyboard(virtualKey, keyUp: false);

        try
        {
            await Task.Delay(holdDuration, cancellationToken);
        }
        finally
        {
            SendKeyboard(virtualKey, keyUp: true);
        }
    }

    public async Task PressMouseButtonAsync(
        MouseInputButton button,
        TimeSpan holdDuration,
        CancellationToken cancellationToken = default)
    {
        if (button == MouseInputButton.None)
        {
            throw new ArgumentOutOfRangeException(nameof(button));
        }

        SendMouseButton(button, buttonDown: true);

        try
        {
            await Task.Delay(holdDuration, cancellationToken);
        }
        finally
        {
            SendMouseButton(button, buttonDown: false);
        }
    }

    public void MouseButtonDown(MouseInputButton button)
    {
        SendMouseButton(button, buttonDown: true);
    }

    public void MouseButtonUp(MouseInputButton button)
    {
        SendMouseButton(button, buttonDown: false);
    }

    public void ScrollMouseWheel(MouseWheelDirection direction)
    {
        int amount = direction switch
        {
            MouseWheelDirection.Up => WheelDelta,
            MouseWheelDirection.Down => -WheelDelta,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };

        INPUT input = new()
        {
            Type = InputMouse,
            Data = new INPUTUNION
            {
                Mouse = new MOUSEINPUT
                {
                    Dx = 0,
                    Dy = 0,
                    MouseData = unchecked((uint)amount),
                    Flags = MouseEventWheel,
                    Time = 0,
                    ExtraInfo = UIntPtr.Zero
                }
            }
        };

        SendSingleInput(input);
    }

    private static void SendKeyboard(
        ushort virtualKey,
        bool keyUp)
    {
        INPUT input = new()
        {
            Type = InputKeyboard,
            Data = new INPUTUNION
            {
                Keyboard = new KEYBDINPUT
                {
                    VirtualKey = virtualKey,
                    ScanCode = 0,
                    Flags = keyUp ? KeyEventKeyUp : 0,
                    Time = 0,
                    ExtraInfo = UIntPtr.Zero
                }
            }
        };

        SendSingleInput(input);
    }

    private static void SendMouseButton(
        MouseInputButton button,
        bool buttonDown)
    {
        uint flags;
        uint mouseData = 0;

        switch (button)
        {
            case MouseInputButton.Left:
                flags = buttonDown
                    ? MouseEventLeftDown
                    : MouseEventLeftUp;
                break;

            case MouseInputButton.Right:
                flags = buttonDown
                    ? MouseEventRightDown
                    : MouseEventRightUp;
                break;

            case MouseInputButton.Middle:
                flags = buttonDown
                    ? MouseEventMiddleDown
                    : MouseEventMiddleUp;
                break;

            case MouseInputButton.XButton1:
                flags = buttonDown
                    ? MouseEventXDown
                    : MouseEventXUp;

                mouseData = XButton1;
                break;

            case MouseInputButton.XButton2:
                flags = buttonDown
                    ? MouseEventXDown
                    : MouseEventXUp;

                mouseData = XButton2;
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(button),
                    button,
                    "Unsupported mouse button.");
        }

        INPUT input = new()
        {
            Type = InputMouse,
            Data = new INPUTUNION
            {
                Mouse = new MOUSEINPUT
                {
                    Dx = 0,
                    Dy = 0,
                    MouseData = mouseData,
                    Flags = flags,
                    Time = 0,
                    ExtraInfo = UIntPtr.Zero
                }
            }
        };

        SendSingleInput(input);
    }

    private static void SendSingleInput(INPUT input)
    {
        uint inserted = SendInput(
            1,
            [input],
            Marshal.SizeOf<INPUT>());

        if (inserted != 1)
        {
            int error = Marshal.GetLastWin32Error();

            throw new Win32Exception(
                error,
                $"SendInput failed. Windows error: {error}.");
        }
    }

    [DllImport(
        "user32.dll",
        SetLastError = true)]
    private static extern uint SendInput(
        uint numberOfInputs,
        INPUT[] inputs,
        int inputSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public INPUTUNION Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION
    {
        [FieldOffset(0)]
        public MOUSEINPUT Mouse;

        [FieldOffset(0)]
        public KEYBDINPUT Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}