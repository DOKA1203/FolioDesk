using System.IO;
using System.Windows;
using FolioDesk.ShortCuts;

namespace FolioDesk;

public partial class MainWindow : Window {
    public MainWindow() {
        InitializeComponent();
    }
    
    private void CreateFolder(object sender, RoutedEventArgs e) {
        var folder = App.DataManager.CreateFolioFolder("New Folder");
        ShortCutManager.CreateShortcut(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "FolioDesk.exe"), folder.Id, $"New AppFolder {folder.Id}");
    }
}