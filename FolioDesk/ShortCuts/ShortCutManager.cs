using System.IO;
using System.Runtime.InteropServices;
using FolioDesk.Icons;
using FolioDesk.Services;
using IWshRuntimeLibrary;

namespace FolioDesk.ShortCuts;

public static class ShortCutManager {
    [DllImport("Shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
    private static readonly string DesktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    
    public static void CreateShortcut(string targetPath,int id, string shortcutName) {
        WshShell? shell = null;
        IWshShortcut? shortcut = null;
        try {
            shell = new WshShell();
            shortcut = (IWshShortcut)shell.CreateShortcut(Path.Combine(DesktopDirectory, $"{shortcutName}.lnk"));
            
            shortcut.TargetPath = targetPath;
            shortcut.Arguments = id.ToString();
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath); // Optional: Set working directory
            shortcut.Description = $"FolioFolder id {id}"; // Optional: Set description
            
            var icoName = IconGenerator.GenerateIcon(id);
            
            shortcut.IconLocation = Path.Combine(App.DataFolder, "icons", id.ToString(), $"{icoName}.ico");
            shortcut.Save();
            AppLogger.Info($"Created shortcut. FolderId={id}, Name='{shortcutName}', Target='{targetPath}', Icon='{icoName}.ico'.");
        }
        catch (Exception ex) {
            AppLogger.Error($"Failed to create shortcut '{shortcutName}' for target '{targetPath}'.", ex);
        }
        finally {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    public static void UpdateShortcut(int folderId, string icoName) {
        WshShell? shell = null;
        try {
            shell = new WshShell();
            var links = Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "*.lnk");
            foreach (var link in links) {
                IWshShortcut? shortcut = null;
                try {
                    shortcut = (IWshShortcut)shell.CreateShortcut(link);
                    if (shortcut.Arguments != $"{folderId}") continue;
                    shortcut.IconLocation = Path.Combine(App.DataFolder, "icons", $"{folderId}", $"{icoName}.ico");
                    shortcut.Save();
                    SHChangeNotify(0x8000000, 0x1000, IntPtr.Zero, IntPtr.Zero);
                    AppLogger.Info($"Updated shortcut icon. FolderId={folderId}, Link='{link}', Icon='{icoName}.ico'.");
                    return;
                }
                catch (Exception ex) {
                    AppLogger.Warning($"Failed to read shortcut '{link}': {ex.Message}");
                }
                finally {
                    ReleaseComObject(shortcut);
                }
            }
            AppLogger.Warning($"No matching shortcut found to update. FolderId={folderId}, Icon='{icoName}.ico'.");
        }
        finally {
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? comObject) {
        if (comObject is not null && Marshal.IsComObject(comObject)) {
            Marshal.FinalReleaseComObject(comObject);
        }
    }
}
