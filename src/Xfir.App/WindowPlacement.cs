// SPDX-License-Identifier: EUPL-1.2
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Xfir.App;

internal static class WindowPlacement
{
    public static void FitToWorkArea(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var source = HwndSource.FromHwnd(handle);
        var monitor = MonitorFromWindow(handle, 2); // MONITOR_DEFAULTTONEAREST
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (source?.CompositionTarget is null || !GetMonitorInfo(monitor, ref info)) return;

        // Win32 uses device pixels; WPF window coordinates use units adjusted for the window's DPI.
        var transform = source.CompositionTarget.TransformFromDevice;
        var topLeft = transform.Transform(new Point(info.Work.Left, info.Work.Top));
        var bottomRight = transform.Transform(new Point(info.Work.Right, info.Work.Bottom));
        var workArea = new Rect(topLeft, bottomRight);
        window.MinWidth = Math.Min(window.MinWidth, workArea.Width);
        window.MinHeight = Math.Min(window.MinHeight, workArea.Height);
        window.Width = Math.Min(window.Width, workArea.Width);
        window.Height = Math.Min(window.Height, workArea.Height);
        // CenterScreen may not have assigned coordinates yet during SourceInitialized.
        if (window.WindowState == WindowState.Normal && double.IsFinite(window.Left))
            window.Left = Math.Clamp(window.Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - window.Width));
        if (window.WindowState == WindowState.Normal && double.IsFinite(window.Top))
            window.Top = Math.Clamp(window.Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - window.Height));
    }

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint MonitorFromWindow(nint window, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect Work;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
