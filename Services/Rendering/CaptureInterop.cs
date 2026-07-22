using System.Runtime.InteropServices;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;

namespace CryoChaos.Services.Rendering;

internal static class CaptureInterop
{
    private static readonly Guid GraphicsCaptureItemGuid =
        new("79C3F95B-31F7-4EC2-A464-632EF5D30760");

    [ComImport]
    [Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        IntPtr CreateForWindow(IntPtr window, [In] ref Guid iid);
        IntPtr CreateForMonitor(IntPtr monitor, [In] ref Guid iid);
    }

    public static GraphicsCaptureItem CreateForWindow(IntPtr hwnd)
    {
        var factory = ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");
        var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factory.ThisPtr);
        Guid iid = GraphicsCaptureItemGuid;
        IntPtr itemPointer = interop.CreateForWindow(hwnd, ref iid);

        try
        {
            return MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer);
        }
        finally
        {
            Marshal.Release(itemPointer);
        }
    }

    public static GraphicsCaptureItem CreateForMonitor(IntPtr monitor)
    {
        var factory = ActivationFactory.Get("Windows.Graphics.Capture.GraphicsCaptureItem");
        var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factory.ThisPtr);
        Guid iid = GraphicsCaptureItemGuid;
        IntPtr itemPointer = interop.CreateForMonitor(monitor, ref iid);

        try
        {
            return MarshalInterface<GraphicsCaptureItem>.FromAbi(itemPointer);
        }
        finally
        {
            Marshal.Release(itemPointer);
        }
    }

    public static IDirect3DDevice CreateWinRtDevice(IntPtr dxgiDevicePointer)
    {
        int hr = NativeMethods.CreateDirect3D11DeviceFromDXGIDevice(
            dxgiDevicePointer,
            out IntPtr devicePointer);

        Marshal.ThrowExceptionForHR(hr);

        try
        {
            return MarshalInterface<IDirect3DDevice>.FromAbi(devicePointer);
        }
        finally
        {
            Marshal.Release(devicePointer);
        }
    }

    [ComImport]
    [Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        [PreserveSig]
        int GetInterface([In] ref Guid iid, out IntPtr result);
    }

    public static IntPtr GetDxgiInterface(IDirect3DSurface surface, Guid interfaceId)
    {
        var access = surface.As<IDirect3DDxgiInterfaceAccess>();
        int hr = access.GetInterface(ref interfaceId, out IntPtr result);
        Marshal.ThrowExceptionForHR(hr);
        return result;
    }
}
