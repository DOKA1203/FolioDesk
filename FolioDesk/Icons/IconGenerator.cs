using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using FolioDesk.Models;
using FolioDesk.Services;

namespace FolioDesk.Icons;

public static class IconGenerator {
    public static string GenerateIcon(FolioFolder folder, string dataFolder, Color? backgroundColor = null) {
        var iconsDir = Path.Combine(dataFolder, "icons", folder.Id.ToString());
        var filePaths = folder.Files
            .OrderBy(item => item.Order)
            .Select(item => item.Icon)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();

        Color bgColor;
        if (backgroundColor.HasValue) {
            bgColor = backgroundColor.Value;
        } else {
            bgColor = ParseIconColor(folder.IconColor);
        }

        using var background = CreateBaseImage(bgColor);
        DrawIconsOnBackground(background, filePaths);

        Directory.CreateDirectory(iconsDir);

        // Guid 기반으로 충돌 없는 파일명 생성
        var fileName = Guid.NewGuid().ToString("N");
        var icoPath = Path.Combine(iconsDir, fileName + ".ico");
        SaveAsIco(background, icoPath);
        AppLogger.Info($"Generated folder icon. FolderId={folder.Id}, Icon='{icoPath}', SourceIconCount={filePaths.Count}.");

        return fileName;
    }

    private static Color ParseIconColor(string? hex) {
        if (string.IsNullOrEmpty(hex)) return Color.FromArgb(255, 216, 216, 216);
        hex = hex.TrimStart('#');
        return hex.Length switch {
            8 => Color.FromArgb(
                Convert.ToByte(hex[0..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16),
                Convert.ToByte(hex[6..8], 16)),
            6 => Color.FromArgb(255,
                Convert.ToByte(hex[0..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16)),
            _ => Color.FromArgb(255, 216, 216, 216)
        };
    }

    private static Bitmap CreateBaseImage(Color fillColor, int cornerRadius = 45) {
        const int size = 256;
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.Clear(Color.Transparent);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var path = BuildRoundedRectPath(new Rectangle(0, 0, size, size), cornerRadius);
        using var brush = new SolidBrush(fillColor);
        g.FillPath(brush, path);
        return bmp;
    }

    private static GraphicsPath BuildRoundedRectPath(Rectangle rect, int radius) {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X,           rect.Y,            d, d, 180, 90);
        path.AddArc(rect.Right - d,   rect.Y,            d, d, 270, 90);
        path.AddArc(rect.Right - d,   rect.Bottom - d,   d, d,   0, 90);
        path.AddArc(rect.X,           rect.Bottom - d,   d, d,  90, 90);
        path.CloseFigure();
        return path;
    }

    private static void DrawIconsOnBackground(Bitmap background, List<string> filePaths) {
        const int iconSize = 110;
        const int padding = 10;
        const int half = 128;

        var positions = new Point[] {
            new(padding,        padding),
            new(half + padding, padding),
            new(padding,        half + padding),
            new(half + padding, half + padding)
        };

        using var g = Graphics.FromImage(background);
        g.CompositingMode    = CompositingMode.SourceOver;
        g.CompositingQuality = CompositingQuality.HighQuality;
        g.InterpolationMode  = InterpolationMode.HighQualityBicubic;
        g.SmoothingMode      = SmoothingMode.HighQuality;

        var count = Math.Min(filePaths.Count, positions.Length);
        for (var i = 0; i < count; i++) {
            if (!File.Exists(filePaths[i])) {
                AppLogger.Warning($"Icon file not found, skipping: {filePaths[i]}");
                continue;
            }

            using var iconImg = new Bitmap(filePaths[i]);
            using var resized = new Bitmap(iconImg, new Size(iconSize, iconSize));
            g.DrawImage(resized, positions[i]);
        }
    }

    public static void CleanupFolderIcons(string dataFolder, int folderId, string iconNameToKeep) {
        var directory = Path.Combine(dataFolder, "icons", folderId.ToString());
        if (!Directory.Exists(directory)) return;

        var keepPath = Path.Combine(directory, $"{iconNameToKeep}.ico");
        var deleted = 0;
        string[] oldIconPaths;
        try {
            oldIconPaths = Directory.GetFiles(directory, "*.ico");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            AppLogger.Warning($"Failed to enumerate old folder icons for {folderId}: {ex.Message}");
            return;
        }

        foreach (var oldPath in oldIconPaths) {
            if (string.Equals(oldPath, keepPath, StringComparison.OrdinalIgnoreCase)) continue;
            try {
                File.Delete(oldPath);
                deleted++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
                AppLogger.Warning($"Failed to delete old folder icon '{oldPath}': {ex.Message}");
            }
        }
        AppLogger.Info($"Cleaned folder icons. FolderId={folderId}, DeletedIcoCount={deleted}.");
    }

    /// <summary>
    /// 256x256 PNG를 ICO 포맷으로 저장.
    /// ICO 스펙: width/height 256은 byte 0으로 표현.
    /// </summary>
    private static void SaveAsIco(Bitmap image, string filePath) {
        const int size = 256;
        const byte icoSize = 0; // ICO 스펙: 256 → 0으로 표기

        using var resized = new Bitmap(image, new Size(size, size));
        using var pngStream = new MemoryStream();
        resized.Save(pngStream, ImageFormat.Png);
        var pngData = pngStream.ToArray();

        using var fs = new FileStream(filePath, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        // ICONDIR (6 bytes)
        bw.Write((short)0);          // Reserved
        bw.Write((short)1);          // Type = ICO
        bw.Write((short)1);          // Image count

        // ICONDIRENTRY (16 bytes)
        bw.Write(icoSize);           // Width  (256 → 0)
        bw.Write(icoSize);           // Height (256 → 0)
        bw.Write((byte)0);           // Color palette (none)
        bw.Write((byte)0);           // Reserved
        bw.Write((short)0);          // Color planes
        bw.Write((short)32);         // Bits per pixel
        bw.Write(pngData.Length);    // PNG data size
        bw.Write(22);                // Data offset (6 + 16 = 22)

        bw.Write(pngData);
    }
}
