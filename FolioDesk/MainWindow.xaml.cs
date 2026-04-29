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
    }

    private void CreateFolder(object sender, RoutedEventArgs e) {
        var folder = App.DataManager.CreateFolioFolder(LocalizationService.Get("DefaultFolderName"));
        var shortcutName = string.Format(LocalizationService.Get("DefaultShortcutName"), folder.Id);
        ShortCutManager.CreateShortcut(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FolioDesk.exe"), folder.Id, shortcutName);
    }
}
