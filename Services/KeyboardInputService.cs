using System.ComponentModel;
using System.Runtime.InteropServices;
using CryoChaos.Models;

namespace CryoChaos.Services;

public sealed class KeyboardInputService
{
    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;

    private const uint KeyEventKeyUp = 0x0002;

    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventMiddleDown = 0x0020;
    private const uint MouseEventMiddleUp = 0x0040;
    private const uint MouseEventXDown = 0x0080;
    private const uint MouseEventXUp = 0x0100;
    private const uint MouseEventWheel = 0x0800;
    private const uint MouseEventMoveNoCoalesce = 0x2000;

    private const uint XButton1 = 0x0001;
    private const uint XButton2 = 0x0002;
    private const int WheelDelta = 120;

    /// <summary>
    /// Presses a parsed Destiny keyboard, mouse-button, or wheel binding.
    /// </summary>
    public async Task PressAsync(
        InputBinding binding,
        TimeSpan holdDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        cancellationToken.ThrowIfCancellationRequested();

        switch (binding.Kind)
        {
            case InputBindingKind.Keyboard:
                await PressKeyboardAsync(
                    binding.VirtualKey,
                    holdDuration,
                    cancellationToken);
                return;

            case InputBindingKind.MouseButton:
                await PressMouseButtonAsync(
                    binding.MouseButton,
                    holdDuration,
                    cancellationToken);
                return;

            case InputBindingKind.MouseWheel:
                SendMouseWheel(binding.WheelDirection);
                return;

            default:
                throw new InvalidOperationException(
                    $"The input binding '{binding.RawValue}' is not supported.");
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
                "The virtual-key code cannot be zero.");
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

    public void SendMouseWheel(MouseWheelDirection direction)
    {
        int delta = direction switch
        {
            MouseWheelDirection.Up => WheelDelta,
            MouseWheelDirection.Down => -WheelDelta,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };

        SendSingle(new Input
        {
            Type = InputMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInput
                {
                    MouseData = unchecked((uint)delta),
                    Flags = MouseEventWheel
                }
            }
        });
    }

    /// <summary>
    /// Moves the mouse by a relative amount immediately.
    /// Positive X moves right, negative X moves left.
    /// Positive Y moves down, negative Y moves up.
    /// </summary>
    public void MoveMouseRelative(int deltaX, int deltaY)
    {
        if (deltaX == 0 && deltaY == 0)
        {
            return;
        }

        SendMouseMovement(deltaX, deltaY);
    }

    /// <summary>
    /// Smoothly distributes relative mouse movement over a duration.
    /// This is useful for camera turns instead of a single sharp jump.
    /// </summary>
    public async Task MoveMouseSmoothAsync(
        int totalDeltaX,
        int totalDeltaY,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (totalDeltaX == 0 && totalDeltaY == 0)
        {
            return;
        }

        if (duration <= TimeSpan.Zero)
        {
            MoveMouseRelative(totalDeltaX, totalDeltaY);
            return;
        }

        const double targetStepMilliseconds = 8.0;

        int stepCount = Math.Max(
            1,
            (int)Math.Ceiling(
                duration.TotalMilliseconds /
                targetStepMilliseconds));

        TimeSpan stepDelay = TimeSpan.FromTicks(
            Math.Max(
                1,
                duration.Ticks / stepCount));

        int sentX = 0;
        int sentY = 0;

        for (int step = 1; step <= stepCount; step++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int targetX = (int)Math.Round(
                totalDeltaX *
                (step / (double)stepCount));

            int targetY = (int)Math.Round(
                totalDeltaY *
                (step / (double)stepCount));

            int stepX = targetX - sentX;
            int stepY = targetY - sentY;

            if (stepX != 0 || stepY != 0)
            {
                SendMouseMovement(stepX, stepY);
                sentX += stepX;
                sentY += stepY;
            }

            if (step < stepCount)
            {
                await Task.Delay(
                    stepDelay,
                    cancellationToken);
            }
        }

        // Correct any rounding remainder so the requested total is exact.
        int remainingX = totalDeltaX - sentX;
        int remainingY = totalDeltaY - sentY;

        if (remainingX != 0 || remainingY != 0)
        {
            SendMouseMovement(remainingX, remainingY);
        }
    }

    private static void SendKeyboard(
        ushort virtualKey,
        bool keyUp)
    {
        SendSingle(new Input
        {
            Type = InputKeyboard,
            Data = new InputUnion
            {
                Keyboard = new KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Flags = keyUp ? KeyEventKeyUp : 0
                }
            }
        });
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
                throw new ArgumentOutOfRangeException(nameof(button));
        }

        SendSingle(new Input
        {
            Type = InputMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInput
                {
                    MouseData = mouseData,
                    Flags = flags
                }
            }
        });
    }

    private static void SendMouseMovement(
        int deltaX,
        int deltaY)
    {
        SendSingle(new Input
        {
            Type = InputMouse,
            Data = new InputUnion
            {
                Mouse = new MouseInput
                {
                    Dx = deltaX,
                    Dy = deltaY,
                    Flags =
                        MouseEventMove |
                        MouseEventMoveNoCoalesce
                }
            }
        });
    }

    private static void SendSingle(Input input)
    {
        uint sent = SendInput(
            1,
            [input],
            Marshal.SizeOf<Input>());

        if (sent != 1)
        {
            int error = Marshal.GetLastWin32Error();

            throw new Win32Exception(
                error,
                $"SendInput failed with Windows error {error}.");
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(
        uint numberOfInputs,
        [MarshalAs(UnmanagedType.LPArray), In] Input[] inputs,
        int sizeOfInputStructure);

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput Mouse;

        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
}
