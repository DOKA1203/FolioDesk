using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using FolioDesk.Infrastructure.Desktop;
using FolioDesk.Services;

namespace FolioDesk;

public partial class App : System.Windows.Application {
    private static readonly Stopwatch StartupClock = Stopwatch.StartNew();
    public static string Version { get; } = ResolveDisplayVersion();
    public static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FolioDesk");

    internal static AppComposition Composition { get; } = new(
        DataFolder,
        Path.Combine(AppContext.BaseDirectory, "FolioDesk.exe"));

    internal static long StartupElapsedMilliseconds => StartupClock.ElapsedMilliseconds;

    private static string ResolveDisplayVersion() {
        var informationalVersion = typeof(App).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var semanticVersion = informationalVersion?.Split('+', 2)[0].Trim();

        if (string.IsNullOrWhiteSpace(semanticVersion)) {
            var assemblyVersion = typeof(App).Assembly.GetName().Version;
            semanticVersion = assemblyVersion is null
                ? "dev"
                : $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}";
        }

        return semanticVersion.Equals("dev", StringComparison.OrdinalIgnoreCase)
            ? semanticVersion
            : $"v{semanticVersion.TrimStart('v', 'V')}";
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
                    ShowFolderAtDesktopIcon(ParseFolderId(e.Args[0]));
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

    private static void ShowFolderAtDesktopIcon(int folderId) {
        var cursorLocated = WindowsPopupPlacement.TryGetCursorPosition(out var cursor);
        var iconLocated = Composition.CreateDesktopIconLocator().TryLocate(folderId, out var iconPosition);
        if (!iconLocated && !cursorLocated)
            throw new InvalidOperationException(LocalizationService.Get("MousePositionError"));

        var anchor = iconLocated ? iconPosition : cursor;
        var cursorForPlacement = cursorLocated ? cursor : anchor;

        var window = new FolioFolderWindow(
            folderId,
            Composition.CreateFolderQueryService(),
            Composition.CreateFolderContentService(),
            Composition.CreateFolderAppearanceService()) {
            WindowStartupLocation = WindowStartupLocation.Manual
        };

        window.SourceInitialized += (_, _) => {
            var windowHandle = new WindowInteropHelper(window).Handle;
            if (!WindowsPopupPlacement.TryMoveToAnchor(windowHandle, anchor))
                AppLogger.Warning($"Initial popup placement failed. FolderId={folderId}, Anchor=({anchor.X},{anchor.Y}).");
        };

        window.Loaded += (_, _) => {
            var windowHandle = new WindowInteropHelper(window).Handle;
            if (!WindowsPopupPlacement.TryPlaceWithinWorkArea(
                    windowHandle,
                    anchor,
                    cursorForPlacement,
                    window.InitialOpenWidth,
                    window.TargetOpenWidth,
                    out var placement)) {
                AppLogger.Warning($"Final popup placement failed. FolderId={folderId}, Anchor=({anchor.X},{anchor.Y}).");
                return;
            }

            var keyboardLaunchLikely = iconLocated && cursorLocated && !placement.CursorInside;
            if (keyboardLaunchLikely)
                window.EnableKeyboardNavigation();

            AppLogger.Info(
                $"Placed folder window. FolderId={folderId}, AnchorSource={(iconLocated ? "DesktopIcon" : "Cursor")}, " +
                $"Anchor=({anchor.X},{anchor.Y}), Bounds=({placement.Bounds.Left},{placement.Bounds.Top}," +
                $"{placement.Bounds.Right},{placement.Bounds.Bottom}), WorkArea=({placement.WorkArea.Left}," +
                $"{placement.WorkArea.Top},{placement.WorkArea.Right},{placement.WorkArea.Bottom}), " +
                $"InputMode={(keyboardLaunchLikely ? "Keyboard" : "Mouse")}."
            );
        };

        window.Show();
        AppLogger.Info(
            $"Folder window shown. FolderId={folderId}, AnchorSource={(iconLocated ? "DesktopIcon" : "Cursor")}, " +
            $"Cursor={(cursorLocated ? $"({cursor.X},{cursor.Y})" : "Unavailable")}, " +
            $"Anchor=({anchor.X},{anchor.Y}).");
    }

    private static void AddItemAndExit(int folderId, string sourcePath) {
        Composition.CreateAddItemService().Add(folderId, sourcePath);
        AppLogger.Info($"Add item command completed. FolderId={folderId}, Source='{sourcePath}', ElapsedMs={StartupElapsedMilliseconds}.");
        Current.Shutdown();
    }
}
