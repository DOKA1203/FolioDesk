using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using FolioDesk.Application.Abstractions;
using FolioDesk.Services;

namespace FolioDesk.Infrastructure.Desktop;

public sealed class WindowsDesktopIconLocator(string executablePath) : IDesktopIconLocator {
    private const int CsidlDesktop = 0;
    private const int SwcDesktop = 8;
    private const int SwfoNeedDispatch = 1;
    private const uint SigdnFileSystemPath = 0x80058000;
    private const int ShellLinkBufferLength = 32768;

    private static readonly Guid SidTopLevelBrowser = new("4C96BE40-915C-11CF-99D3-00AA004AE837");
    private readonly string _executablePath = Path.GetFullPath(executablePath);

    public bool TryLocate(int folderId, out PhysicalScreenPoint position) {
        var stopwatch = Stopwatch.StartNew();
        position = default;

        try {
            var found = TryLocateInDesktopView(folderId, out position, out var source);
            AppLogger.Info(found
                ? $"Located desktop icon. FolderId={folderId}, Source={source}, Position=({position.X},{position.Y}), ElapsedMs={stopwatch.ElapsedMilliseconds}."
                : $"Desktop view did not expose an icon position. FolderId={folderId}, ElapsedMs={stopwatch.ElapsedMilliseconds}.");
            return found;
        }
        catch (Exception ex) {
            AppLogger.Warning($"Desktop icon lookup failed for folder {folderId}: {ex.Message}");
            return false;
        }
    }

    private List<string> FindShortcutPaths(int folderId) {
        var matches = new List<string>();
        var desktopDirectories = new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        }.Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var desktopDirectory in desktopDirectories) {
            string[] shortcutPaths;
            try {
                if (!Directory.Exists(desktopDirectory))
                    continue;

                shortcutPaths = Directory.GetFiles(desktopDirectory, "*.lnk");
            }
            catch (Exception ex) {
                AppLogger.Warning($"Failed to enumerate desktop directory '{desktopDirectory}' while locating an icon: {ex.Message}");
                continue;
            }

            foreach (var path in shortcutPaths) {
                if (ShortcutMatches(path, folderId))
                    matches.Add(path);
            }
        }

        return matches;
    }

    private bool TryLocateInDesktopView(
        int folderId,
        out PhysicalScreenPoint position,
        out string source) {
        position = default;
        source = "None";
        object? shellWindowsObject = null;
        object? desktopDispatch = null;
        object? browserObject = null;
        object? viewObject = null;
        object? folderObject = null;

        try {
            shellWindowsObject = new ShellWindows();
            var shellWindows = (IShellWindows)shellWindowsObject;
            object desktopLocation = CsidlDesktop;
            object desktopRoot = null!;
            desktopDispatch = shellWindows.FindWindowSW(
                ref desktopLocation,
                ref desktopRoot,
                SwcDesktop,
                out _,
                SwfoNeedDispatch);

            var serviceProvider = (IComServiceProvider)desktopDispatch;
            var service = SidTopLevelBrowser;
            var browserInterface = typeof(IShellBrowser).GUID;
            browserObject = serviceProvider.QueryService(ref service, ref browserInterface);
            var browser = (IShellBrowser)browserObject;
            viewObject = browser.QueryActiveShellView();
            var shellView = (IShellView)viewObject;
            var folderView = (IFolderView)viewObject;

            if (shellView.GetWindow(out var viewWindow) < 0 || viewWindow == IntPtr.Zero)
                return false;

            var folderInterface = typeof(IShellFolder).GUID;
            if (folderView.GetFolder(ref folderInterface, out folderObject) < 0)
                return false;
            var desktopFolder = (IShellFolder)folderObject;

            try {
                if (TryGetFocusedShortcutPosition(
                        folderView,
                        desktopFolder,
                        viewWindow,
                        out var focusedPath,
                        out var focusedPosition) &&
                    ShortcutMatches(focusedPath, folderId)) {
                    position = focusedPosition;
                    source = "FocusedItem";
                    return true;
                }
            }
            catch (Exception ex) {
                AppLogger.Warning($"Focused desktop item lookup failed; scanning shortcuts instead: {ex.Message}");
            }

            var shortcutPaths = FindShortcutPaths(folderId);
            if (shortcutPaths.Count == 0) {
                AppLogger.Warning($"Desktop icon lookup found no shortcut for folder {folderId} targeting '{_executablePath}'.");
                return false;
            }

            foreach (var shortcutPath in shortcutPaths) {
                try {
                    if (TryGetParsedShortcutPosition(
                            folderView,
                            desktopFolder,
                            viewWindow,
                            shortcutPath,
                            out position)) {
                        source = "DesktopScan";
                        return true;
                    }
                }
                catch (Exception ex) {
                    AppLogger.Warning($"Failed to read desktop position for shortcut '{shortcutPath}': {ex.Message}");
                }
            }

            return false;
        }
        finally {
            ReleaseComObject(folderObject);
            ReleaseComObject(viewObject);
            ReleaseComObject(browserObject);
            ReleaseComObject(desktopDispatch);
            ReleaseComObject(shellWindowsObject);
        }
    }

    private static bool TryGetFocusedShortcutPosition(
        IFolderView folderView,
        IShellFolder desktopFolder,
        IntPtr viewWindow,
        out string shortcutPath,
        out PhysicalScreenPoint position) {
        shortcutPath = string.Empty;
        position = default;
        if (folderView.GetFocusedItem(out var focusedIndex) < 0 || focusedIndex < 0)
            return false;
        if (folderView.Item(focusedIndex, out var itemIdList) < 0 || itemIdList == IntPtr.Zero)
            return false;

        try {
            var focusedPath = TryGetFileSystemPath(desktopFolder, itemIdList);
            if (string.IsNullOrWhiteSpace(focusedPath) ||
                !focusedPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!TryGetScreenPosition(folderView, viewWindow, itemIdList, out position))
                return false;

            shortcutPath = focusedPath;
            return true;
        }
        finally {
            Marshal.FreeCoTaskMem(itemIdList);
        }
    }

    private static string? TryGetFileSystemPath(IShellFolder parentFolder, IntPtr itemIdList) {
        var shellItemInterface = typeof(IShellItem).GUID;
        if (SHCreateItemWithParent(
                IntPtr.Zero,
                parentFolder,
                itemIdList,
                ref shellItemInterface,
                out var shellItemObject) < 0) {
            return null;
        }

        try {
            var shellItem = (IShellItem)shellItemObject;
            if (shellItem.GetDisplayName(SigdnFileSystemPath, out var text) < 0 || text == IntPtr.Zero)
                return null;

            try {
                return Marshal.PtrToStringUni(text);
            }
            finally {
                Marshal.FreeCoTaskMem(text);
            }
        }
        finally {
            ReleaseComObject(shellItemObject);
        }
    }

    private static bool TryGetParsedShortcutPosition(
        IFolderView folderView,
        IShellFolder desktopFolder,
        IntPtr viewWindow,
        string shortcutPath,
        out PhysicalScreenPoint position) {
        position = default;
        uint charactersEaten = 0;
        uint attributes = 0;
        var itemIdList = IntPtr.Zero;

        try {
            var result = desktopFolder.ParseDisplayName(
                IntPtr.Zero,
                IntPtr.Zero,
                Path.GetFileName(shortcutPath),
                ref charactersEaten,
                out itemIdList,
                ref attributes);
            return result >= 0 && itemIdList != IntPtr.Zero &&
                   TryGetScreenPosition(folderView, viewWindow, itemIdList, out position);
        }
        finally {
            if (itemIdList != IntPtr.Zero)
                Marshal.FreeCoTaskMem(itemIdList);
        }
    }

    private static bool TryGetScreenPosition(
        IFolderView folderView,
        IntPtr viewWindow,
        IntPtr itemIdList,
        out PhysicalScreenPoint position) {
        position = default;
        if (folderView.GetItemPosition(itemIdList, out var point) < 0)
            return false;
        if (!ClientToScreen(viewWindow, ref point))
            return false;

        position = new PhysicalScreenPoint(point.X, point.Y);
        return true;
    }

    private bool ShortcutMatches(string shortcutPath, int folderId) {
        try {
            if (!TryReadShortcut(shortcutPath, out var targetPath, out var arguments))
                return false;

            return arguments.Trim().Equals(
                       folderId.ToString(CultureInfo.InvariantCulture),
                       StringComparison.Ordinal) &&
                   PathsEqual(targetPath, _executablePath);
        }
        catch (Exception ex) {
            AppLogger.Warning($"Failed to inspect desktop shortcut '{shortcutPath}' while locating its icon: {ex.Message}");
            return false;
        }
    }

    private static bool TryReadShortcut(
        string shortcutPath,
        out string targetPath,
        out string arguments) {
        targetPath = string.Empty;
        arguments = string.Empty;
        object? shellLinkObject = null;

        try {
            shellLinkObject = new ShellLink();
            var persistFile = (IPersistFile)shellLinkObject;
            persistFile.Load(shortcutPath, 0);

            var shellLink = (IShellLinkW)shellLinkObject;
            var targetBuffer = new StringBuilder(ShellLinkBufferLength);
            var argumentsBuffer = new StringBuilder(ShellLinkBufferLength);
            if (shellLink.GetPath(targetBuffer, targetBuffer.Capacity, IntPtr.Zero, 0) < 0 ||
                shellLink.GetArguments(argumentsBuffer, argumentsBuffer.Capacity) < 0) {
                return false;
            }

            targetPath = targetBuffer.ToString();
            arguments = argumentsBuffer.ToString();
            return !string.IsNullOrWhiteSpace(targetPath);
        }
        finally {
            ReleaseComObject(shellLinkObject);
        }
    }

    private static bool PathsEqual(string left, string right) {
        try {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException) {
            return false;
        }
    }

    private static void ReleaseComObject(object? value) {
        if (value is not null && Marshal.IsComObject(value))
            Marshal.FinalReleaseComObject(value);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr windowHandle, ref NativePoint point);

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHCreateItemWithParent(
        IntPtr parentIdList,
        [MarshalAs(UnmanagedType.Interface)] IShellFolder parentFolder,
        IntPtr childIdList,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out object shellItem);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint {
        public int X;
        public int Y;
    }

    [ComImport]
    [Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class ShellWindows;

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class ShellLink;

    [ComImport]
    [Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85")]
    [InterfaceType(ComInterfaceType.InterfaceIsIDispatch)]
    private interface IShellWindows {
        [return: MarshalAs(UnmanagedType.IDispatch)]
        object FindWindowSW(
            [MarshalAs(UnmanagedType.Struct)] ref object location,
            [MarshalAs(UnmanagedType.Struct)] ref object root,
            int windowClass,
            out int windowHandle,
            int options);
    }

    [ComImport]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IComServiceProvider {
        [return: MarshalAs(UnmanagedType.Interface)]
        object QueryService(ref Guid service, ref Guid interfaceId);
    }

    [ComImport]
    [Guid("000214E2-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellBrowser {
        void Gap01();
        void Gap02();
        void Gap03();
        void Gap04();
        void Gap05();
        void Gap06();
        void Gap07();
        void Gap08();
        void Gap09();
        void Gap10();
        void Gap11();
        void Gap12();

        [return: MarshalAs(UnmanagedType.Interface)]
        object QueryActiveShellView();
    }

    [ComImport]
    [Guid("000214E3-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellView {
        [PreserveSig]
        int GetWindow(out IntPtr windowHandle);
    }

    [ComImport]
    [Guid("CDE725B0-CCC9-4519-917E-325D72FAB4CE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFolderView {
        [PreserveSig] int GetCurrentViewMode(out uint mode);
        [PreserveSig] int SetCurrentViewMode(uint mode);
        [PreserveSig] int GetFolder(ref Guid interfaceId, [MarshalAs(UnmanagedType.Interface)] out object folder);
        [PreserveSig] int Item(int index, out IntPtr itemIdList);
        [PreserveSig] int ItemCount(uint flags, out int count);
        [PreserveSig] int Items(uint flags, ref Guid interfaceId, out IntPtr value);
        [PreserveSig] int GetSelectionMarkedItem(out int index);
        [PreserveSig] int GetFocusedItem(out int index);
        [PreserveSig] int GetItemPosition(IntPtr itemIdList, out NativePoint point);
    }

    [ComImport]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellFolder {
        [PreserveSig]
        int ParseDisplayName(
            IntPtr windowHandle,
            IntPtr bindContext,
            [MarshalAs(UnmanagedType.LPWStr)] string displayName,
            ref uint charactersEaten,
            out IntPtr itemIdList,
            ref uint attributes);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem {
        void GapBindToHandler();
        void GapGetParent();

        [PreserveSig]
        int GetDisplayName(uint displayNameType, out IntPtr text);
    }

    [ComImport]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLinkW {
        [PreserveSig]
        int GetPath(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder filePath,
            int filePathLength,
            IntPtr findData,
            uint flags);

        [PreserveSig]
        int GetIdList(out IntPtr itemIdList);

        [PreserveSig]
        int SetIdList(IntPtr itemIdList);

        [PreserveSig]
        int GetDescription(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder description,
            int descriptionLength);

        [PreserveSig]
        int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string description);

        [PreserveSig]
        int GetWorkingDirectory(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder workingDirectory,
            int workingDirectoryLength);

        [PreserveSig]
        int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string workingDirectory);

        [PreserveSig]
        int GetArguments(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments,
            int argumentsLength);
    }
}
