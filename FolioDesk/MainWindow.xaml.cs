using System.Diagnostics;
using System.IO;
using System.Windows;
using FolioDesk.Services;
using FolioDesk.ShortCuts;

namespace FolioDesk;

public partial class MainWindow : Window {
    public MainWindow() {
        InitializeComponent();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e) {
        if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) {
        Application.Current.Shutdown();
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
        var folder = App.DataManager.CreateFolioFolder(LocalizationService.Get("DefaultFolderName"));
        var shortcutName = string.Format(LocalizationService.Get("DefaultShortcutName"), folder.Id);
        ShortCutManager.CreateShortcut(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FolioDesk.exe"), folder.Id, shortcutName);
        AppLogger.Info($"Create folder command completed. FolderId={folder.Id}, ShortcutName='{shortcutName}'.");
    }
}
