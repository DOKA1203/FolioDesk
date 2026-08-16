using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using FolioDesk.Services;

namespace FolioDesk;

public partial class App : System.Windows.Application {
    private static readonly Stopwatch StartupClock = Stopwatch.StartNew();
    public static readonly string Version = "v1.1.1";
    public static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FolioDesk");

    internal static AppComposition Composition { get; } = new(
        DataFolder,
        Path.Combine(AppContext.BaseDirectory, "FolioDesk.exe"));

    internal static long StartupElapsedMilliseconds => StartupClock.ElapsedMilliseconds;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out PointNative point);

    [StructLayout(LayoutKind.Sequential)]
    private struct PointNative {
        public int X;
        public int Y;
    }

    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);
        AppLogger.Initialize(DataFolder);
        LocalizationService.Initialize();
        AppLogger.Info($"Starting FolioDesk {Version}. Args={e.Args.Length}, InitializationMs={StartupElapsedMilliseconds}.");

        try {
            switch (e.Args.Length) {
                case 0:
                    new MainWindow(Composition.CreateFolderService()).Show();
                    AppLogger.Info("Main window shown.");
                    break;

                case 1:
                    ShowFolderAtCursor(ParseFolderId(e.Args[0]));
                    break;

                case 2:
                    AddItemAndExit(ParseFolderId(e.Args[0]), e.Args[1]);
                    break;

                default:
                    throw new ArgumentException("FolioDesk received an unsupported number of arguments.");
            }
        }
        catch (Exception ex) {
            AppLogger.Error("FolioDesk startup command failed.", ex);
            MessageBox.Show(ex.Message, "FolioDesk", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static int ParseFolderId(string value) {
        if (!int.TryParse(value, out var folderId) || folderId <= 0)
            throw new ArgumentException($"Invalid folder ID: '{value}'.", nameof(value));
        return folderId;
    }

    private static void ShowFolderAtCursor(int folderId) {
        if (!GetCursorPos(out var cursor))
            throw new InvalidOperationException(LocalizationService.Get("MousePositionError"));

        var window = new FolioFolderWindow(
            folderId,
            Composition.CreateFolderQueryService(),
            Composition.CreateFolderContentService(),
            Composition.CreateFolderAppearanceService()) {
            WindowStartupLocation = WindowStartupLocation.Manual
        };

        window.SourceInitialized += (_, _) => {
            var source = PresentationSource.FromVisual(window);
            if (source?.CompositionTarget == null) return;
            var point = source.CompositionTarget.TransformFromDevice.Transform(new Point(cursor.X, cursor.Y));
            window.Left = point.X;
            window.Top = point.Y;
        };

        window.Show();
        AppLogger.Info($"Folder window shown. FolderId={folderId}, Cursor=({cursor.X},{cursor.Y}).");
    }

    private static void AddItemAndExit(int folderId, string sourcePath) {
        Composition.CreateAddItemService().Add(folderId, sourcePath);
        AppLogger.Info($"Add item command completed. FolderId={folderId}, Source='{sourcePath}', ElapsedMs={StartupElapsedMilliseconds}.");
        Current.Shutdown();
    }
}
