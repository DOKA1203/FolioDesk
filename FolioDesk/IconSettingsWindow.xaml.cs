using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FolioDesk.Icons;
using FolioDesk.ShortCuts;

namespace FolioDesk;

public partial class IconSettingsWindow : Window {
    private readonly int _folderId;
    private double _hue;   // 0–360
    private double _sat;   // 0–1
    private double _val;   // 0–1
    private bool _isDraggingSv;
    private bool _isDraggingHue;
    private bool _updatingFromHsv;
    private readonly GradientStop _svHueStop = new(Colors.Red, 1.0);

    public IconSettingsWindow(int folderId) {
        InitializeComponent();

        // 브러시를 코드로 생성해 GradientStop을 직접 제어 (XAML 명명 시 frozen 문제 방지)
        var hueBrush = new LinearGradientBrush();
        hueBrush.StartPoint = new Point(0, 0);
        hueBrush.EndPoint   = new Point(1, 0);
        hueBrush.GradientStops.Add(new GradientStop(Colors.White, 0));
        hueBrush.GradientStops.Add(_svHueStop);
        SvHueRect.Fill = hueBrush;

        _folderId = folderId;
        var folder = App.DataManager.GetFolioFolder(folderId);
        var (rgb, alpha) = ParseFolderColor(folder?.IconColor);
        OpacitySlider.Value = Math.Round(alpha / 255.0 * 100);
        (_hue, _sat, _val) = RgbToHsv(rgb);
        Loaded += (_, _) => SyncFromHsv();
    }

    // ── HSV → UI sync ────────────────────────────────────────────────────────

    private void SyncFromHsv() {
        _updatingFromHsv = true;
        var color = HsvToRgb(_hue, _sat, _val);
        UpdateSvThumb();
        UpdateHueThumb();
        _svHueStop.Color = HsvToRgb(_hue, 1, 1);
        UpdatePreview(color);
        HexInput.Text = $"{color.R:X2}{color.G:X2}{color.B:X2}";
        _updatingFromHsv = false;
    }

    private void UpdateSvThumb() {
        if (SvGrid.ActualWidth == 0) return;
        Canvas.SetLeft(SvThumb, _sat * SvGrid.ActualWidth - 7);
        Canvas.SetTop(SvThumb, (1 - _val) * SvGrid.ActualHeight - 7);
    }

    private void UpdateHueThumb() {
        if (HueGrid.ActualWidth == 0) return;
        Canvas.SetLeft(HueThumb, _hue / 360.0 * HueGrid.ActualWidth - 3);
    }

    private void UpdatePreview(Color color) {
        var alpha = (byte)Math.Round(OpacitySlider.Value / 100.0 * 255);
        PreviewBorder.Background = new SolidColorBrush(
            Color.FromArgb(alpha, color.R, color.G, color.B));
    }

    // ── SV 캔버스 인터랙션 ───────────────────────────────────────────────────

    private void SvGrid_MouseDown(object sender, MouseButtonEventArgs e) {
        _isDraggingSv = true;
        SvGrid.CaptureMouse();
        ApplySvPoint(e.GetPosition(SvGrid));
    }

    private void SvGrid_MouseMove(object sender, MouseEventArgs e) {
        if (!_isDraggingSv) return;
        ApplySvPoint(e.GetPosition(SvGrid));
    }

    private void SvGrid_MouseUp(object sender, MouseButtonEventArgs e) {
        _isDraggingSv = false;
        SvGrid.ReleaseMouseCapture();
    }

    private void ApplySvPoint(Point p) {
        _sat = Math.Clamp(p.X / SvGrid.ActualWidth, 0, 1);
        _val = Math.Clamp(1 - p.Y / SvGrid.ActualHeight, 0, 1);
        SyncFromHsv();
    }

    // ── Hue 바 인터랙션 ──────────────────────────────────────────────────────

    private void HueGrid_MouseDown(object sender, MouseButtonEventArgs e) {
        _isDraggingHue = true;
        HueGrid.CaptureMouse();
        ApplyHuePoint(e.GetPosition(HueGrid));
    }

    private void HueGrid_MouseMove(object sender, MouseEventArgs e) {
        if (!_isDraggingHue) return;
        ApplyHuePoint(e.GetPosition(HueGrid));
    }

    private void HueGrid_MouseUp(object sender, MouseButtonEventArgs e) {
        _isDraggingHue = false;
        HueGrid.ReleaseMouseCapture();
    }

    private void ApplyHuePoint(Point p) {
        _hue = Math.Clamp(p.X / HueGrid.ActualWidth * 360, 0, 360);
        SyncFromHsv();
    }

    // ── Hex 입력 ─────────────────────────────────────────────────────────────

    private void HexInput_PreviewTextInput(object sender, TextCompositionEventArgs e) {
        e.Handled = !Regex.IsMatch(e.Text, "^[0-9A-Fa-f]+$");
    }

    private void HexInput_TextChanged(object sender, TextChangedEventArgs e) {
        if (_updatingFromHsv || HexInput.Text.Length != 6) return;
        try {
            var c = (Color)ColorConverter.ConvertFromString("#" + HexInput.Text);
            (_hue, _sat, _val) = RgbToHsv(c);
            SyncFromHsv();
        } catch { }
    }

    // ── 투명도 ───────────────────────────────────────────────────────────────

    private void OpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
        if (OpacityLabel == null) return;
        OpacityLabel.Text = $"{(int)e.NewValue}%";
        UpdatePreview(HsvToRgb(_hue, _sat, _val));
    }

    // ── 적용 ─────────────────────────────────────────────────────────────────

    private void Apply_Click(object sender, RoutedEventArgs e) {
        var color = HsvToRgb(_hue, _sat, _val);
        var alpha = (byte)Math.Round(OpacitySlider.Value / 100.0 * 255);
        App.DataManager.UpdateFolderColor(_folderId, $"#{alpha:X2}{color.R:X2}{color.G:X2}{color.B:X2}");
        var icoName = IconGenerator.GenerateIcon(_folderId);
        ShortCutManager.UpdateShortcut(_folderId, icoName);
        Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    // ── 색상 변환 ─────────────────────────────────────────────────────────────

    private static (double H, double S, double V) RgbToHsv(Color c) {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        double v = max;
        double s = max == 0 ? 0 : delta / max;
        double h = 0;
        if (delta != 0) {
            if (max == r)      h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * ((b - r) / delta + 2);
            else               h = 60 * ((r - g) / delta + 4);
            if (h < 0) h += 360;
        }
        return (h, s, v);
    }

    private static Color HsvToRgb(double h, double s, double v) {
        if (s == 0) {
            var gray = (byte)(v * 255);
            return Color.FromRgb(gray, gray, gray);
        }
        h %= 360;
        int i = (int)(h / 60);
        double f = h / 60 - i;
        double p = v * (1 - s);
        double q = v * (1 - s * f);
        double t = v * (1 - s * (1 - f));
        var (r, g, b) = i switch {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static (Color rgb, byte alpha) ParseFolderColor(string? hex) {
        if (string.IsNullOrEmpty(hex)) return (Color.FromRgb(124, 108, 242), 255);
        try {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            return (Color.FromRgb(c.R, c.G, c.B), c.A);
        } catch {
            return (Color.FromRgb(124, 108, 242), 255);
        }
    }
}