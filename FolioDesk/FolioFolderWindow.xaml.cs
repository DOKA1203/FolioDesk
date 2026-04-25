
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;


namespace FolioDesk;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class FolioFolderWindow : Window {
    public FolioFolderWindow(int folderId) {
        InitializeComponent();
        Deactivated += OtherWindow_Deactivated!;
        
        var folder = App.DataManager.GetFolioFolder(folderId);
        var appIcons = new List<AppIcon>();

        if (folder != null) {
            foreach (var file in folder.Files.OrderBy(f => f.Order)) {
                appIcons.Add(new AppIcon(file.Name, file.Path, file.Icon));
            }
        }
        
        const double itemWidth = 80.0;
        int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(appIcons.Count)));
        AppFolderPanel.Width = cols * itemWidth;
        AppFolderPanel.ItemsSource = appIcons;
    }
    
    
    
    
    private void OtherWindow_Deactivated(object sender, EventArgs e) {
        if (!IsMouseOver) {
            Close();
        }
    }
    private class AppIcon {
        public string Name { get; set; }
        public string LnkPath { get; set; }
        public BitmapImage Icon { get; set; }

        public AppIcon(string name, string lnkPath, string absoluteIconPath) {
            Name = name;
            Icon = new BitmapImage(new Uri(absoluteIconPath, UriKind.Absolute));
            LnkPath = lnkPath;
        }
    }
    private void Icon_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) {
        e.Handled = true;

        if (sender is Border border && border.DataContext is AppIcon icon) {
            ShellExecute(IntPtr.Zero, "open", icon.LnkPath, "", "", 1);
            Close();
        }
    }
    [DllImport("Shell32.dll")]
    private static extern int ShellExecute(IntPtr hwnd, string lpOperation, string lpFile, string lpParameters, string lpDirectory, int nShowCmd);
}