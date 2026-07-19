using System.ComponentModel;
using System.Runtime.InteropServices;
using CryoChaos.Models;

namespace CryoChaos.Services;

internal static class MagnificationNative
{
    internal const string MagnifierWindowClass = "Magnifier";

    internal const uint WsChild = 0x40000000;
    internal const uint WsVisible = 0x10000000;

    internal const uint WsExTransparent = 0x00000020;
    internal const uint WsExToolWindow = 0x00000080;
    internal const uint WsExNoActivate = 0x08000000;

    internal const uint SwpNoZOrder = 0x0004;
    internal const uint SwpNoActivate = 0x0010;
    internal const uint SwpShowWindow = 0x0040;

    internal const uint MwFilterModeExclude = 0;

    private static readonly object RuntimeLock = new();
    private static int _runtimeUsers;

    internal static void AcquireRuntime()
    {
        if (!Environment.Is64BitProcess)
        {
            throw new PlatformNotSupportedException(
                "Live screen-transform effects require the x64 CryoChaos build.");
        }

        lock (RuntimeLock)
        {
            if (_runtimeUsers == 0 && !MagInitialize())
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Windows Magnification API initialization failed.");
            }

            _runtimeUsers++;
        }
    }

    internal static void ReleaseRuntime()
    {
        lock (RuntimeLock)
        {
            if (_runtimeUsers <= 0)
            {
                return;
            }

            _runtimeUsers--;

            if (_runtimeUsers == 0)
            {
                MagUninitialize();
            }
        }
    }

    internal static MagTransform CreateTransform(
        ScreenTransformMode mode,
        int sourceWidth,
        int sourceHeight,
        int destinationWidth,
        int destinationHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceWidth),
                "The source rectangle must have a positive size.");
        }

        if (destinationWidth <= 0 || destinationHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(destinationWidth),
                "The destination surface must have a positive size.");
        }

        bool quarterTurn =
            mode is ScreenTransformMode.Rotate90Clockwise or
                ScreenTransformMode.Rotate90CounterClockwise;

        float rotatedWidth = quarterTurn ? sourceHeight : sourceWidth;
        float rotatedHeight = quarterTurn ? sourceWidth : sourceHeight;

        // Fill the full destination window. This can crop a small amount on
        // displays whose aspect ratio differs from the captured game window.
        float scale = MathF.Max(
            destinationWidth / rotatedWidth,
            destinationHeight / rotatedHeight);

        float offsetX = (destinationWidth - rotatedWidth * scale) / 2f;
        float offsetY = (destinationHeight - rotatedHeight * scale) / 2f;

        return mode switch
        {
            ScreenTransformMode.Rotate180 => new MagTransform
            {
                M00 = -scale,
                M01 = 0,
                M02 = sourceWidth * scale + offsetX,
                M10 = 0,
                M11 = -scale,
                M12 = sourceHeight * scale + offsetY,
                M20 = 0,
                M21 = 0,
                M22 = 1
            },

            ScreenTransformMode.Rotate90Clockwise => new MagTransform
            {
                M00 = 0,
                M01 = -scale,
                M02 = sourceHeight * scale + offsetX,
                M10 = scale,
                M11 = 0,
                M12 = offsetY,
                M20 = 0,
                M21 = 0,
                M22 = 1
            },

            ScreenTransformMode.Rotate90CounterClockwise => new MagTransform
            {
                M00 = 0,
                M01 = scale,
                M02 = offsetX,
                M10 = -scale,
                M11 = 0,
                M12 = sourceWidth * scale + offsetY,
                M20 = 0,
                M21 = 0,
                M22 = 1
            },

            ScreenTransformMode.FlipHorizontal => new MagTransform
            {
                M00 = -scale,
                M01 = 0,
                M02 = sourceWidth * scale + offsetX,
                M10 = 0,
                M11 = scale,
                M12 = offsetY,
                M20 = 0,
                M21 = 0,
                M22 = 1
            },

            ScreenTransformMode.FlipVertical => new MagTransform
            {
                M00 = scale,
                M01 = 0,
                M02 = offsetX,
                M10 = 0,
                M11 = -scale,
                M12 = sourceHeight * scale + offsetY,
                M20 = 0,
                M21 = 0,
                M22 = 1
            },

            _ => new MagTransform
            {
                M00 = scale,
                M01 = 0,
                M02 = offsetX,
                M10 = 0,
                M11 = scale,
                M12 = offsetY,
                M20 = 0,
                M21 = 0,
                M22 = 1
            }
        };
    }

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MagSetWindowSource(
        IntPtr magnifierWindow,
        NativeRect sourceRectangle);

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MagSetWindowTransform(
        IntPtr magnifierWindow,
        ref MagTransform transform);

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MagSetWindowFilterList(
        IntPtr magnifierWindow,
        uint filterMode,
        int count,
        [In] IntPtr[] windows);

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MagShowSystemCursor(
        [MarshalAs(UnmanagedType.Bool)] bool showCursor);

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagInitialize();

    [DllImport("Magnification.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MagUninitialize();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr CreateWindowEx(
        uint extendedStyle,
        string className,
        string windowName,
        uint style,
        int x,
        int y,
        int width,
        int height,
        IntPtr parent,
        IntPtr menu,
        IntPtr instance,
        IntPtr parameter);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(IntPtr window);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetWindowPos(
        IntPtr window,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetClientRect(
        IntPtr window,
        out NativeRect rectangle);

    [DllImport("user32.dll", SetLastError = false)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool InvalidateRect(
        IntPtr window,
        IntPtr rectangle,
        [MarshalAs(UnmanagedType.Bool)] bool erase);
}

[StructLayout(LayoutKind.Sequential)]
internal struct MagTransform
{
    public float M00;
    public float M01;
    public float M02;

    public float M10;
    public float M11;
    public float M12;

    public float M20;
    public float M21;
    public float M22;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeRect
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;

    public readonly int Width => Right - Left;
    public readonly int Height => Bottom - Top;

    public override readonly string ToString() =>
        $"{Left},{Top} - {Right},{Bottom} ({Width}x{Height})";
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativePoint
{
    public int X;
    public int Y;
}
