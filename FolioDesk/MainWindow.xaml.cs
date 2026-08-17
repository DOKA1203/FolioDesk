using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using FolioDesk.Application;
using FolioDesk.Services;

namespace FolioDesk;

public partial class MainWindow : Window {
    private const int DwmWindowCornerPreference = 33;
    private const int DwmSystemBackdropType = 38;
    private const int DwmUseImmersiveDarkMode = 20;
    private const int DwmCornerRound = 2;
    private const int DwmBackdropMicaAlt = 4;

    private readonly CreateFolderService _createFolderService;

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    public MainWindow() : this(App.Composition.CreateFolderService()) { }

    internal MainWindow(CreateFolderService createFolderService) {
        _createFolderService = createFolderService;
        InitializeComponent();
        SourceInitialized += (_, _) => ApplyWindows11Backdrop();
        ContentRendered += (_, _) =>
            AppLogger.Info($"Main window content rendered. StartupElapsedMs={App.StartupElapsedMilliseconds}.");
    }

    private void ApplyWindows11Backdrop() {
        var windowHandle = new WindowInteropHelper(this).Handle;
        if (windowHandle == IntPtr.Zero) return;

        if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) {
            SetDwmAttribute(windowHandle, DwmUseImmersiveDarkMode, 1);
            SetDwmAttribute(windowHandle, DwmWindowCornerPreference, DwmCornerRound);
        }

        // DWMWA_SYSTEMBACKDROP_TYPE supports Mica Alt from Windows 11 22H2 (build 22621).
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621)) {
            AppLogger.Info("Mica Alt is unavailable. Main window is using the opaque fallback surface.");
            return;
        }

        if (!SetDwmAttribute(windowHandle, DwmSystemBackdropType, DwmBackdropMicaAlt)) {
            AppLogger.Warning("DWM rejected the Mica Alt backdrop. Main window is using the opaque fallback surface.");
            return;
        }

        if (HwndSource.FromHwnd(windowHandle)?.CompositionTarget is { } compositionTarget)
            compositionTarget.BackgroundColor = Colors.Transparent;

        Background = Brushes.Transparent;
        RootShell.SetResourceReference(BackgroundProperty, "MicaShellBrush");
        AppLogger.Info("Mica Alt backdrop applied to the main window.");
    }

    private static bool SetDwmAttribute(IntPtr windowHandle, int attribute, int value) =>
        DwmSetWindowAttribute(windowHandle, attribute, ref value, sizeof(int)) == 0;

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) {
        System.Windows.Application.Current.Shutdown();
    }

    private void ToggleLang_Click(object sender, RoutedEventArgs e) {
        LocalizationService.ToggleLanguage();
        AppLogger.Info($"Language toggled. CurrentLang={LocalizationService.CurrentLang}.");
    }

    private void CheckUpdate_Click(object sender, RoutedEventArgs e) {
        Process.Start(new ProcessStartInfo("https://github.com/doka1203/FolioDesk/") { UseShellExecute = true });
        AppLogger.Info("Opened update page.");
    }

    private void CreateFolder(object sender, RoutedEventArgs e) {
        try {
            var folderName = LocalizationService.Get("DefaultFolderName");
            var shortcutTemplate = LocalizationService.Get("DefaultShortcutName");
            var folder = _createFolderService.Create(folderName, shortcutTemplate);
            AppLogger.Info($"Create folder command completed. FolderId={folder.Id}.");
        }
        catch (Exception ex) {
            AppLogger.Error("Create folder command failed.", ex);
            MessageBox.Show(ex.Message, "FolioDesk", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
