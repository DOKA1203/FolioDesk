using System.Runtime.InteropServices;
using FolioDesk.Application.Abstractions;

namespace FolioDesk.Infrastructure.Desktop;

internal readonly record struct PhysicalScreenRect(int Left, int Top, int Right, int Bottom) {
    public int Width => Right - Left;
    public int Height => Bottom - Top;

    public bool Contains(PhysicalScreenPoint point) =>
        point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom;
}

internal readonly record struct PopupPlacementResult(
    PhysicalScreenRect Bounds,
    PhysicalScreenRect WorkArea,
    bool CursorInside);

internal static class WindowsPopupPlacement {
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint MonitorDefaultToNearest = 0x00000002;

    public static bool TryGetCursorPosition(out PhysicalScreenPoint position) {
        if (GetCursorPos(out var point)) {
            position = new PhysicalScreenPoint(point.X, point.Y);
            return true;
        }

        position = default;
        return false;
    }

    public static bool TryMoveToAnchor(IntPtr windowHandle, PhysicalScreenPoint anchor) =>
        windowHandle != IntPtr.Zero && SetWindowPos(
            windowHandle,
            IntPtr.Zero,
            anchor.X,
            anchor.Y,
            0,
            0,
            SwpNoSize | SwpNoZOrder | SwpNoActivate);

    public static bool TryPlaceWithinWorkArea(
        IntPtr windowHandle,
        PhysicalScreenPoint anchor,
        PhysicalScreenPoint cursor,
        double initialWidth,
        double targetWidth,
        out PopupPlacementResult result) {
        result = default;
        if (windowHandle == IntPtr.Zero || initialWidth <= 0 || targetWidth <= 0)
            return false;
        if (!GetWindowRect(windowHandle, out var currentRect))
            return false;

        var monitor = MonitorFromPoint(new NativePoint(anchor.X, anchor.Y), MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
            return false;

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
            return false;

        var currentWidth = Math.Max(1, currentRect.Right - currentRect.Left);
        var currentHeight = Math.Max(1, currentRect.Bottom - currentRect.Top);
        var targetWidthPixels = Math.Max(1, (int)Math.Ceiling(currentWidth * targetWidth / initialWidth));
        var workArea = new PhysicalScreenRect(
            monitorInfo.WorkArea.Left,
            monitorInfo.WorkArea.Top,
            monitorInfo.WorkArea.Right,
            monitorInfo.WorkArea.Bottom);
        var bounds = ClampToWorkArea(anchor, targetWidthPixels, currentHeight, workArea);

        if (!SetWindowPos(
                windowHandle,
                IntPtr.Zero,
                bounds.Left,
                bounds.Top,
                0,
                0,
                SwpNoSize | SwpNoZOrder | SwpNoActivate))
            return false;

        result = new PopupPlacementResult(bounds, workArea, bounds.Contains(cursor));
        return true;
    }

    internal static PhysicalScreenRect ClampToWorkArea(
        PhysicalScreenPoint anchor,
        int width,
        int height,
        PhysicalScreenRect workArea) {
        width = Math.Max(1, width);
        height = Math.Max(1, height);

        var maximumLeft = Math.Max(workArea.Left, workArea.Right - width);
        var maximumTop = Math.Max(workArea.Top, workArea.Bottom - height);
        var left = Math.Clamp(anchor.X, workArea.Left, maximumLeft);
        var top = Math.Clamp(anchor.Y, workArea.Top, maximumTop);
        return new PhysicalScreenRect(left, top, left + width, top + height);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr windowHandle, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo monitorInfo);

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint(int x, int y) {
        public readonly int X = x;
        public readonly int Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
    }
}
