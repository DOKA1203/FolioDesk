using System.Configuration;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using FolioDesk.Icons;
using FolioDesk.Models;
using FolioDesk.Services;
using FolioDesk.ShortCuts;

namespace FolioDesk;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application {
    public static readonly string Version = "v1.0.0";
    public static readonly string DataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FolioDesk");
    public static readonly FolioDataManager DataManager = CreateDataManager();

    private static FolioDataManager CreateDataManager() {
        AppLogger.Initialize(DataFolder);
        return new FolioDataManager();
    }
    
    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT {
        public int X;
        public int Y;
    }
    
    // 지정한 폴더 ID에 해당하는 폴더 팝업을 커서 위치에 표시
    private static void ShowWindowAtCursor(int folderId) {
        if (!GetCursorPos(out var cursor)) {
            MessageBox.Show(LocalizationService.Get("MousePositionError"));
            return;
        }

        var window = new FolioFolderWindow(folderId) { WindowStartupLocation = WindowStartupLocation.Manual };

        window.SourceInitialized += (s, e) => {
            var source = PresentationSource.FromVisual(window);
            if (source?.CompositionTarget == null) return;
            var transform = source.CompositionTarget.TransformFromDevice;

            // 물리 픽셀 → WPF 좌표(DIP)
            var point = transform.Transform(new Point(cursor.X, cursor.Y));

            window.Left = point.X;
            window.Top = point.Y;
        };

        window.Show();
        AppLogger.Info($"Folder window shown. FolderId={folderId}, Cursor=({cursor.X},{cursor.Y}).");
    }

    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);
        LocalizationService.Initialize();
        AppLogger.Info($"Starting FolioDesk {Version}. Args: {e.Args.Length}");

        switch (e.Args.Length) {
            case 0:
                // 인수 없음: 메인 관리 창 표시 (폴더 목록 관리 UI)
                var main = new MainWindow();
                main.Show();
                AppLogger.Info("Main window shown.");
                break;

            case 1:
                // 인수 1개: args[0] = 폴더 ID
                // 해당 폴더를 커서 위치에 팝업으로 표시
                ShowWindowAtCursor(int.Parse(e.Args[0]));
                AppLogger.Info($"Folder popup requested. FolderId={e.Args[0]}.");
                break;

            case 2:
                // 인수 2개: args[0] = 폴더 ID, args[1] = 추가할 실행 파일 경로
                // 실행 파일의 아이콘을 추출해 DataFolder에 저장하고
                // FolioData에 FolioItem을 추가한 뒤 종료
                var addedApplication = e.Args[1]!;
                var folderId = int.Parse(e.Args[0]!);
                
                var folder = DataManager.GetFolioFolder(folderId);
                if (folder == null) {
                    AppLogger.Warning($"Folder ID {folderId} was not found while adding '{addedApplication}'.");
                    return;
                }

                var appName = GetUniqueAppName(folder, Path.GetFileNameWithoutExtension(addedApplication));
                var appDirectory = Path.Combine(DataFolder, "icons", $"{folderId}", appName);
                var pngPath = Path.Combine(appDirectory, "icon.png");
                var storedPath = Path.Combine(appDirectory, Path.GetFileName(addedApplication));
                Directory.CreateDirectory(appDirectory);
                
                try {
                    var movedFromDesktop = MoveOrCopyToFolderStorage(addedApplication, storedPath);
                    IconExtractor.SaveIconAsPng(storedPath, pngPath);

                    DataManager.AddFileToFolder(folderId, new FolioItem {
                        Icon = pngPath,
                        Name = appName,
                        Path = storedPath
                    });

                    var icoName = IconGenerator.GenerateIcon(folderId);
                    
                    ShortCutManager.UpdateShortcut(folderId, icoName);
                    AppLogger.Info($"Added application to folder. FolderId={folderId}, Name='{appName}', Source='{addedApplication}', Stored='{storedPath}', MovedFromDesktop={movedFromDesktop}.");
                }
                catch (Exception ex) {
                    AppLogger.Error($"Failed to add application '{addedApplication}' to folder {folderId}.", ex);
                }
                
                
                
                Shutdown();
                break;
        }
    }

    private static bool MoveOrCopyToFolderStorage(string sourcePath, string destinationPath) {
        if (IsInDesktopDirectory(sourcePath)) {
            File.Move(sourcePath, destinationPath, overwrite: false);
            return true;
        }

        File.Copy(sourcePath, destinationPath, overwrite: false);
        return false;
    }

    private static string GetUniqueAppName(FolioFolder folder, string baseName) {
        var usedNames = folder.Files.Select(file => file.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var folderIconDirectory = Path.Combine(DataFolder, "icons", $"{folder.Id}");

        var candidate = baseName;
        var counter = 2;
        while (usedNames.Contains(candidate) || Directory.Exists(Path.Combine(folderIconDirectory, candidate))) {
            candidate = $"{baseName} ({counter})";
            counter++;
        }

        return candidate;
    }

    private static bool IsInDesktopDirectory(string path) {
        var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrWhiteSpace(sourceDirectory)) return false;

        return IsSameDirectory(sourceDirectory, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)) ||
               IsSameDirectory(sourceDirectory, Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory));
    }

    private static bool IsSameDirectory(string left, string right) {
        if (string.IsNullOrWhiteSpace(right)) return false;

        var normalizedLeft = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
        var normalizedRight = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
        return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }
}
