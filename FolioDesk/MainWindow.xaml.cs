using System.Diagnostics;
using System.Windows;
using FolioDesk.Application;
using FolioDesk.Services;

namespace FolioDesk;

public partial class MainWindow : Window {
    private readonly CreateFolderService _createFolderService;

    public MainWindow() : this(App.Composition.CreateFolderService()) { }

    internal MainWindow(CreateFolderService createFolderService) {
        _createFolderService = createFolderService;
        InitializeComponent();
        ContentRendered += (_, _) =>
            AppLogger.Info($"Main window content rendered. StartupElapsedMs={App.StartupElapsedMilliseconds}.");
    }

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
