
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using FolioDesk.Application;
using FolioDesk.Models;
using FolioDesk.Services;


namespace FolioDesk;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class FolioFolderWindow : Window {
    private const string DragFormat = "FolioDesk.AppIcon";

    private readonly int _folderId;
    private readonly FolderQueryService _queryService;
    private readonly FolderContentService _contentService;
    private readonly FolderAppearanceService _appearanceService;
    private readonly ObservableCollection<AppIcon> _appIcons = [];
    private Point _dragStartPoint;
    private Point _grabOffset;
    private bool _isDragging;
    private bool _justDragged;
    private bool _droppedInternally;
    private bool _settingsOpen;
    private bool _keyboardNavigationEnabled;
    private AppIcon? _keyboardSelectedIcon;
    private Image? _dragGhost;

    private const double ItemWidth = 80.0;
    private const double FramePadding = 16.0;
    private const int SingleRowMax = 5;

    internal double InitialOpenWidth { get; private set; }
    internal double TargetOpenWidth { get; private set; }

    public FolioFolderWindow(int folderId) : this(
        folderId,
        App.Composition.CreateFolderQueryService(),
        App.Composition.CreateFolderContentService(),
        App.Composition.CreateFolderAppearanceService()) { }

    internal FolioFolderWindow(
        int folderId,
        FolderQueryService queryService,
        FolderContentService contentService,
        FolderAppearanceService appearanceService) {
        _queryService = queryService;
        _contentService = contentService;
        _appearanceService = appearanceService;
        InitializeComponent();
        ContentRendered += (_, _) =>
            AppLogger.Info($"Folder window content rendered. FolderId={folderId}, StartupElapsedMs={App.StartupElapsedMilliseconds}.");
        Deactivated += OtherWindow_Deactivated!;
        _folderId = folderId;

        var folder = _queryService.GetFolder(folderId);

        if (folder != null) {
            foreach (var file in folder.Files.OrderBy(f => f.Order)) {
                _appIcons.Add(new AppIcon(file));
            }
        }

        ApplyContentWidth();
        AppFolderPanel.ItemsSource = _appIcons;

        TargetOpenWidth = AppFolderPanel.Width + FramePadding;
        InitialOpenWidth = Math.Min(ItemWidth + FramePadding, TargetOpenWidth);
        Width = InitialOpenWidth;

        if (TargetOpenWidth > InitialOpenWidth + 0.5) {
            Loaded += (_, _) => AnimateOpenWidth(InitialOpenWidth, TargetOpenWidth);
        }
    }

    private void ApplyContentWidth() {
        AppFolderPanel.Width = GetColumnCount(_appIcons.Count) * ItemWidth;
    }

    private static int GetColumnCount(int itemCount) {
        var rows = itemCount <= SingleRowMax ? 1 : 2;
        return Math.Max(1, (int)Math.Ceiling(itemCount / (double)rows));
    }

    private void AnimateOpenWidth(double from, double to) {
        var anim = new DoubleAnimation {
            From = from,
            To = to,
            Duration = TimeSpan.FromMilliseconds(220),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        anim.Completed += (_, _) => {
            BeginAnimation(WidthProperty, null);
            Width = to;
        };
        BeginAnimation(WidthProperty, anim);
    }


    private void OtherWindow_Deactivated(object sender, EventArgs e) {
        if (_isDragging || _settingsOpen) return;
        if (!IsMouseOver) {
            Close();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) {
        _settingsOpen = true;
        var settingsWindow = new IconSettingsWindow(_folderId, _queryService, _appearanceService) {
            Owner = this,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        settingsWindow.ShowDialog();
        _settingsOpen = false;
    }
    private class AppIcon {
        public FolioItem Item { get; }
        public string Name => Item.Name;
        public string LnkPath => Item.Path;
        public BitmapImage Icon { get; }

        public AppIcon(FolioItem item) {
            Item = item;
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.UriSource = new Uri(item.Icon, UriKind.Absolute);
            bmp.DecodePixelWidth = 48;
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.EndInit();
            bmp.Freeze();
            Icon = bmp;
        }
    }

    private void Icon_PreviewMouseDown(object sender, MouseButtonEventArgs e) {
        ExitKeyboardNavigation();
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
        _droppedInternally = false;
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

        if (!_droppedInternally && IsCursorOutsideWindow()) {
            ExtractToDesktop(icon);
        }
    }

    private bool IsCursorOutsideWindow() {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return false;
        if (!GetCursorPos(out var pt)) return false;
        if (!GetWindowRect(hwnd, out var rect)) return false;
        return pt.X < rect.Left || pt.X > rect.Right || pt.Y < rect.Top || pt.Y > rect.Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT pt);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    private void ExtractToDesktop(AppIcon icon) {
        try {
            var destination = _contentService.ExtractToDesktop(_folderId, icon.Item);
            _appIcons.Remove(icon);
            if (ReferenceEquals(_keyboardSelectedIcon, icon))
                _keyboardSelectedIcon = null;

            ApplyContentWidth();
            Width = AppFolderPanel.Width + FramePadding;
            AppLogger.Info($"Extract UI updated. FolderId={_folderId}, Name='{icon.Name}', Destination='{destination}'.");
        }
        catch (Exception ex) {
            AppLogger.Error($"Failed to extract '{icon.Name}' from folder {_folderId} to desktop.", ex);
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
        _droppedInternally = true;
        if (ReferenceEquals(dragged, target)) return;

        int oldIndex = _appIcons.IndexOf(dragged);
        int newIndex = _appIcons.IndexOf(target);
        if (oldIndex < 0 || newIndex < 0) return;

        _appIcons.Move(oldIndex, newIndex);
        try {
            _contentService.Reorder(_folderId, _appIcons.Select(app => app.Item).ToList());
        }
        catch (Exception ex) {
            _appIcons.Move(newIndex, oldIndex);
            AppLogger.Error($"Failed to reorder folder {_folderId}.", ex);
        }
    }

    private void Icon_Click(object sender, MouseButtonEventArgs e) {
        if (_justDragged) return;
        e.Handled = true;

        if (sender is Border border && border.DataContext is AppIcon icon) {
            LaunchIcon(icon);
        }
    }

    internal void EnableKeyboardNavigation() {
        _keyboardNavigationEnabled = true;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () => {
            if (IsVisible && _appIcons.Count > 0)
                FocusKeyboardIcon(_appIcons[0]);
        });
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
        if (_isDragging || _settingsOpen) return;

        if (e.Key == Key.Escape) {
            e.Handled = true;
            Close();
            return;
        }

        if (!_keyboardNavigationEnabled) return;

        switch (e.Key) {
            case Key.Left:
            case Key.Right:
            case Key.Up:
            case Key.Down:
                e.Handled = true;
                MoveKeyboardSelection(e.Key);
                break;

            case Key.Enter:
                if (_keyboardSelectedIcon is not null) {
                    e.Handled = true;
                    LaunchIcon(_keyboardSelectedIcon);
                }
                break;
        }
    }

    private void Window_PreviewMouseMove(object sender, MouseEventArgs e) {
        if (_keyboardSelectedIcon is not null && !_isDragging)
            ExitKeyboardNavigation();
    }

    private void MoveKeyboardSelection(Key key) {
        if (_appIcons.Count == 0) return;
        if (_keyboardSelectedIcon is null) {
            FocusKeyboardIcon(_appIcons[0]);
            return;
        }

        var currentIndex = _appIcons.IndexOf(_keyboardSelectedIcon);
        if (currentIndex < 0) {
            FocusKeyboardIcon(_appIcons[0]);
            return;
        }

        var columns = GetColumnCount(_appIcons.Count);
        var column = currentIndex % columns;
        var targetIndex = key switch {
            Key.Left when column > 0 => currentIndex - 1,
            Key.Right when column + 1 < columns && currentIndex + 1 < _appIcons.Count => currentIndex + 1,
            Key.Up when currentIndex - columns >= 0 => currentIndex - columns,
            Key.Down when currentIndex + columns < _appIcons.Count => currentIndex + columns,
            _ => currentIndex
        };

        if (targetIndex != currentIndex)
            FocusKeyboardIcon(_appIcons[targetIndex]);
    }

    private void FocusKeyboardIcon(AppIcon icon) {
        var index = _appIcons.IndexOf(icon);
        if (index < 0) return;

        AppFolderPanel.UpdateLayout();
        if (AppFolderPanel.ItemContainerGenerator.ContainerFromIndex(index) is not ContentPresenter container)
            return;

        _keyboardSelectedIcon = icon;
        Keyboard.Focus(container);
    }

    private void ExitKeyboardNavigation() {
        if (_keyboardSelectedIcon is null) return;
        _keyboardSelectedIcon = null;
        Keyboard.Focus(AppFolderPanel);
    }

    private void LaunchIcon(AppIcon icon) {
        ShellExecute(IntPtr.Zero, "open", icon.LnkPath, "", "", 1);
        Close();
    }

    [DllImport("Shell32.dll")]
    private static extern int ShellExecute(IntPtr hwnd, string lpOperation, string lpFile, string lpParameters, string lpDirectory, int nShowCmd);
}
