using System.IO;
using System.Runtime.InteropServices;
using FolioDesk.Application.Abstractions;
using FolioDesk.Services;

namespace FolioDesk.ShortCuts;

public sealed class WindowsShortcutService(string dataFolder) : IShortcutService {
    [DllImport("Shell32.dll")]
    private static extern void SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

    private static readonly string DesktopDirectory =
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    public void CreateFolderShortcut(
        string targetPath,
        int folderId,
        string shortcutName,
        string iconName) {
        object? shell = null;
        object? shortcut = null;
        try {
            shell = CreateShell();
            dynamic shellApi = shell;
            shortcut = shellApi.CreateShortcut(Path.Combine(DesktopDirectory, $"{shortcutName}.lnk"));
            dynamic shortcutApi = shortcut;
            shortcutApi.TargetPath = targetPath;
            shortcutApi.Arguments = folderId.ToString();
            shortcutApi.WorkingDirectory = Path.GetDirectoryName(targetPath);
            shortcutApi.Description = $"FolioFolder id {folderId}";
            shortcutApi.IconLocation = GetIconPath(folderId, iconName);
            shortcutApi.Save();
            AppLogger.Info($"Created shortcut. FolderId={folderId}, Name='{shortcutName}', Target='{targetPath}', Icon='{iconName}.ico'.");
        }
        finally {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    public void UpdateFolderShortcut(int folderId, string iconName) {
        object? shell = null;
        try {
            shell = CreateShell();
            dynamic shellApi = shell;
            foreach (var link in Directory.GetFiles(DesktopDirectory, "*.lnk")) {
                object? shortcut = null;
                try {
                    shortcut = shellApi.CreateShortcut(link);
                    dynamic shortcutApi = shortcut;
                    if ((string)shortcutApi.Arguments != folderId.ToString()) continue;
                    shortcutApi.IconLocation = GetIconPath(folderId, iconName);
                    shortcutApi.Save();
                    SHChangeNotify(0x08000000, 0x1000, IntPtr.Zero, IntPtr.Zero);
                    AppLogger.Info($"Updated shortcut icon. FolderId={folderId}, Link='{link}', Icon='{iconName}.ico'.");
                    return;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or COMException) {
                    AppLogger.Warning($"Failed to inspect shortcut '{link}': {ex.Message}");
                }
                finally {
                    ReleaseComObject(shortcut);
                }
            }
            throw new FileNotFoundException($"No desktop shortcut was found for FolioDesk folder {folderId}.");
        }
        finally {
            ReleaseComObject(shell);
        }
    }

    private string GetIconPath(int folderId, string iconName) =>
        Path.Combine(dataFolder, "icons", folderId.ToString(), $"{iconName}.ico");

    private static object CreateShell() {
        var shellType = Type.GetTypeFromProgID("WScript.Shell") ??
                        throw new PlatformNotSupportedException("Windows Script Host is unavailable.");
        return Activator.CreateInstance(shellType) ??
               throw new COMException("Windows Script Host could not be created.");
    }

    private static void ReleaseComObject(object? comObject) {
        if (comObject is not null && Marshal.IsComObject(comObject))
            Marshal.FinalReleaseComObject(comObject);
    }
}
