
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FolioDesk.Models;


namespace FolioDesk;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class FolioFolderWindow : Window {
    private const string DragFormat = "FolioDesk.AppIcon";

    private readonly int _folderId;
    private readonly ObservableCollection<AppIcon> _appIcons = [];
    private Point _dragStartPoint;
    private Point _grabOffset;
    private bool _isDragging;
    private bool _justDragged;
    private Image? _dragGhost;

    public FolioFolderWindow(int folderId) {
        InitializeComponent();
        Deactivated += OtherWindow_Deactivated!;
        _folderId = folderId;

        var folder = App.DataManager.GetFolioFolder(folderId);

        if (folder != null) {
            foreach (var file in folder.Files.OrderBy(f => f.Order)) {
                _appIcons.Add(new AppIcon(file));
            }
        }

        const double itemWidth = 80.0;
        int cols = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(_appIcons.Count)));
        AppFolderPanel.Width = cols * itemWidth;
        AppFolderPanel.ItemsSource = _appIcons;
    }


    private void OtherWindow_Deactivated(object sender, EventArgs e) {
        if (_isDragging) return;
        if (!IsMouseOver) {
            Close();
        }
    }
    private class AppIcon {
        public FolioItem Item { get; }
        public string Name => Item.Name;
        public string LnkPath => Item.Path;
        public BitmapImage Icon { get; }

        public AppIcon(FolioItem item) {
            Item = item;
            Icon = new BitmapImage(new Uri(item.Icon, UriKind.Absolute));
        }
    }

    private void Icon_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
        _justDragged = false;
        _dragStartPoint = e.GetPosition(null);
        if (sender is Border border) {
            _grabOffset = e.GetPosition(border);
        }
    }

    private void Icon_MouseMove(object sender, MouseEventArgs e) {
        if (e.LeftButton != MouseButtonState.Pressed || _isDragging) return;
        if (sender is not Border border || border.DataContext is not AppIcon icon) return;

        var current = e.GetPosition(null);
        var diff = _dragStartPoint - current;
        if (Math.Abs(diff.X) <= SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) <= SystemParameters.MinimumVerticalDragDistance) {
            return;
        }

        _isDragging = true;
        StartDragGhost(border);
        try {
            var data = new DataObject(DragFormat, icon);
            DragDrop.DoDragDrop(border, data, DragDropEffects.Move);
        }
        finally {
            EndDragGhost();
            _isDragging = false;
            _justDragged = true;
        }
    }

    private void StartDragGhost(Border source) {
        int w = (int)Math.Ceiling(source.ActualWidth);
        int h = (int)Math.Ceiling(source.ActualHeight);
        if (w <= 0 || h <= 0) return;

        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(source);

        _dragGhost = new Image {
            Source = bmp,
            Width = source.ActualWidth,
            Height = source.ActualHeight,
            Opacity = 0.85,
            IsHitTestVisible = false,
        };
        DragLayer.Children.Add(_dragGhost);
        PreviewDragOver += Window_PreviewDragOver;
    }

    private void EndDragGhost() {
        PreviewDragOver -= Window_PreviewDragOver;
        if (_dragGhost != null) {
            DragLayer.Children.Remove(_dragGhost);
            _dragGhost = null;
        }
    }

    private void Window_PreviewDragOver(object sender, DragEventArgs e) {
        if (_dragGhost == null) return;
        var pos = e.GetPosition(DragLayer);
        Canvas.SetLeft(_dragGhost, pos.X - _grabOffset.X);
        Canvas.SetTop(_dragGhost, pos.Y - _grabOffset.Y);

        if (e.Data.GetDataPresent(DragFormat)) {
            e.Effects = DragDropEffects.Move;
        }
    }

    private void Icon_DragOver(object sender, DragEventArgs e) {
        e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void Icon_Drop(object sender, DragEventArgs e) {
        e.Handled = true;
        if (e.Data.GetData(DragFormat) is not AppIcon dragged) return;
        if (sender is not Border border || border.DataContext is not AppIcon target) return;
        if (ReferenceEquals(dragged, target)) return;

        int oldIndex = _appIcons.IndexOf(dragged);
        int newIndex = _appIcons.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0) return;

        _appIcons.Move(oldIndex, newIndex);
        App.DataManager.ReorderFiles(_folderId, _appIcons.Select(a => a.Item).ToList());
    }

    private void Icon_Click(object sender, MouseButtonEventArgs e) {
        if (_justDragged) return;
        e.Handled = true;

        if (sender is Border border && border.DataContext is AppIcon icon) {
            ShellExecute(IntPtr.Zero, "open", icon.LnkPath, "", "", 1);
            Close();
        }
    }
    [DllImport("Shell32.dll")]
    private static extern int ShellExecute(IntPtr hwnd, string lpOperation, string lpFile, string lpParameters, string lpDirectory, int nShowCmd);
}