using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using CryoChaos.Models;
using CryoChaos.Services;

namespace CryoChaos.Views;

/// <summary>
/// Hosts the native Windows Magnifier control inside WPF. The magnifier reads
/// a live desktop rectangle and applies a native 3x3 rotation/flip matrix.
/// </summary>
public sealed class MagnifierHost : HwndHost
{
    private IntPtr _hostWindow;
    private IntPtr _magnifierWindow;
    private bool _runtimeAcquired;

    private NativeRect _sourceRectangle;
    private ScreenTransformMode _mode;
    private IntPtr[] _excludedWindows = [];
    private bool _hasConfiguration;

    internal void Configure(
        NativeRect sourceRectangle,
        ScreenTransformMode mode,
        IEnumerable<IntPtr> excludedWindows)
    {
        _sourceRectangle = sourceRectangle;
        _mode = mode;
        _excludedWindows = excludedWindows
            .Where(handle => handle != IntPtr.Zero)
            .Distinct()
            .ToArray();
        _hasConfiguration = true;

        ApplyConfiguration();
    }


    internal void SetExcludedWindows(IEnumerable<IntPtr> excludedWindows)
    {
        _excludedWindows = excludedWindows
            .Where(handle => handle != IntPtr.Zero)
            .Distinct()
            .ToArray();

        ApplyConfiguration();
    }

    internal void UpdateSourceRectangle(NativeRect sourceRectangle)
    {
        _sourceRectangle = sourceRectangle;

        if (_magnifierWindow != IntPtr.Zero)
        {
            if (!MagnificationNative.MagSetWindowSource(
                    _magnifierWindow,
                    sourceRectangle))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "The magnifier source rectangle could not be updated.");
            }
        }
    }

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        MagnificationNative.AcquireRuntime();
        _runtimeAcquired = true;

        try
        {
            _hostWindow = MagnificationNative.CreateWindowEx(
                0,
                "static",
                "CryoChaosMagnifierHost",
                MagnificationNative.WsChild |
                MagnificationNative.WsVisible,
                0,
                0,
                1,
                1,
                hwndParent.Handle,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (_hostWindow == IntPtr.Zero)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "The native magnifier host window could not be created.");
            }

            _magnifierWindow = MagnificationNative.CreateWindowEx(
                0,
                MagnificationNative.MagnifierWindowClass,
                "CryoChaosLiveTransform",
                MagnificationNative.WsChild |
                MagnificationNative.WsVisible,
                0,
                0,
                1,
                1,
                _hostWindow,
                IntPtr.Zero,
                IntPtr.Zero,
                IntPtr.Zero);

            if (_magnifierWindow == IntPtr.Zero)
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "The Windows Magnifier control could not be created.");
            }

            ApplyConfiguration();
            return new HandleRef(this, _hostWindow);
        }
        catch
        {
            DestroyNativeWindows();
            ReleaseRuntime();
            throw;
        }
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        DestroyNativeWindows();
        ReleaseRuntime();
    }

    protected override void OnWindowPositionChanged(Rect rcBoundingBox)
    {
        base.OnWindowPositionChanged(rcBoundingBox);
        ResizeMagnifierToHost();
        ApplyConfiguration();
    }

    private void ResizeMagnifierToHost()
    {
        if (_hostWindow == IntPtr.Zero ||
            _magnifierWindow == IntPtr.Zero ||
            !MagnificationNative.GetClientRect(
                _hostWindow,
                out NativeRect clientRectangle))
        {
            return;
        }

        int width = Math.Max(1, clientRectangle.Width);
        int height = Math.Max(1, clientRectangle.Height);

        MagnificationNative.SetWindowPos(
            _magnifierWindow,
            IntPtr.Zero,
            0,
            0,
            width,
            height,
            MagnificationNative.SwpNoZOrder |
            MagnificationNative.SwpNoActivate |
            MagnificationNative.SwpShowWindow);
    }

    private void ApplyConfiguration()
    {
        if (!_hasConfiguration ||
            _hostWindow == IntPtr.Zero ||
            _magnifierWindow == IntPtr.Zero)
        {
            return;
        }

        ResizeMagnifierToHost();

        if (!MagnificationNative.GetClientRect(
                _hostWindow,
                out NativeRect destinationRectangle) ||
            destinationRectangle.Width <= 0 ||
            destinationRectangle.Height <= 0)
        {
            return;
        }

        if (!MagnificationNative.MagSetWindowSource(
                _magnifierWindow,
                _sourceRectangle))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The Windows Magnifier source rectangle could not be set.");
        }

        MagTransform transform = MagnificationNative.CreateTransform(
            _mode,
            _sourceRectangle.Width,
            _sourceRectangle.Height,
            destinationRectangle.Width,
            destinationRectangle.Height);

        if (!MagnificationNative.MagSetWindowTransform(
                _magnifierWindow,
                ref transform))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "The live screen transformation could not be applied.");
        }

        if (_excludedWindows.Length > 0 &&
            !MagnificationNative.MagSetWindowFilterList(
                _magnifierWindow,
                MagnificationNative.MwFilterModeExclude,
                _excludedWindows.Length,
                _excludedWindows))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "CryoChaos windows could not be excluded from the live capture.");
        }
    }

    private void DestroyNativeWindows()
    {
        if (_magnifierWindow != IntPtr.Zero)
        {
            MagnificationNative.DestroyWindow(_magnifierWindow);
            _magnifierWindow = IntPtr.Zero;
        }

        if (_hostWindow != IntPtr.Zero)
        {
            MagnificationNative.DestroyWindow(_hostWindow);
            _hostWindow = IntPtr.Zero;
        }
    }

    private void ReleaseRuntime()
    {
        if (!_runtimeAcquired)
        {
            return;
        }

        _runtimeAcquired = false;
        MagnificationNative.ReleaseRuntime();
    }
}
