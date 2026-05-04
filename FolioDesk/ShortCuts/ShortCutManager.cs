using System.IO;
using System.Runtime.InteropServices;
using FolioDesk.Icons;
using IWshRuntimeLibrary;

namespace FolioDesk.ShortCuts;

public class ShortCutManager {
    [DllImport("Shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);
    private static readonly string DesktopDirectory = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    
    public static void CreateShortcut(string targetPath,int id, string shortcutName) {
        try {
            var shell = new WshShell();
            var shortcut = (IWshShortcut)shell.CreateShortcut(Path.Combine(DesktopDirectory, $"{shortcutName}.lnk"));
            
            shortcut.TargetPath = targetPath;
            shortcut.Arguments = id.ToString();
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath); // Optional: Set working directory
            shortcut.Description = $"FolioFolder id {id}"; // Optional: Set description
            
            var icoName = IconGenerator.GenerateIcon(id);
            
            shortcut.IconLocation = Path.Combine(App.DataFolder, "icons", id.ToString(), $"{icoName}.ico");
            shortcut.Save();
        }
        catch (Exception ex) {
            Console.WriteLine($"Error creating shortcut: {ex.Message}");
        }
    }

    public static void UpdateShortcut(int folderId, string icoName) {
        var shell = new WshShell();
        var links = Directory.GetFiles(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "*.lnk");
        foreach (var link in links) {
            var shortcut = (IWshShortcut)shell.CreateShortcut(link);
            if (shortcut.Arguments != $"{folderId}") continue;
            shortcut.IconLocation = Path.Combine(App.DataFolder, "icons", $"{folderId}", $"{icoName}.ico");
            shortcut.Save();
            SHChangeNotify(0x8000000, 0x1000, IntPtr.Zero, IntPtr.Zero);
            return;
        }
    }
}