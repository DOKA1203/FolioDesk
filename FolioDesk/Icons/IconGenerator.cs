using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;

namespace FolioDesk.Icons;

public static class IconGenerator {
    public static string GenerateIcon(int folderId) {
        var iconsDir = Path.Combine(App.DataFolder, "icons", $"{folderId}");
        var filePaths = GetFilePaths(folderId);

        using var background = LoadBaseImage();
        DrawIconsOnBackground(background, filePaths);

        // 기존 .ico 제거 후 디렉토리 보장
        EnsureCleanDirectory(iconsDir);

        // Guid 기반으로 충돌 없는 파일명 생성
        var fileName = Guid.NewGuid().ToString("N");
        var icoPath = Path.Combine(iconsDir, fileName + ".ico");
        SaveAsIco(background, icoPath);

        return fileName;
    }

    private static Bitmap LoadBaseImage() {
        var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("FolioDesk.Resources.base.png")
            ?? throw new FileNotFoundException("Embedded resource 'FolioDesk.Resources.base.png' not found.");
        return new Bitmap(stream);
    }

    private static List<string> GetFilePaths(int folderId) {
        var folder = App.DataManager.GetFolioFolder(folderId);

        if (folder is null) {
            // Console 대신 예외로 명확하게 알림
            throw new InvalidOperationException(
                $"Folder with ID {folderId} not found or has no files.");
        }

        return folder.Files
            .Select(f => f.Icon)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToList();
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
                Console.Error.WriteLine($"Icon file not found, skipping: {filePaths[i]}");
                continue;
            }

            using var iconImg = new Bitmap(filePaths[i]);
            using var resized = new Bitmap(iconImg, new Size(iconSize, iconSize));
            g.DrawImage(resized, positions[i]);
        }
    }

    private static void EnsureCleanDirectory(string dir) {
        if (Directory.Exists(dir)) {
            foreach (var old in Directory.GetFiles(dir, "*.ico"))
                File.Delete(old);
        }
        else {
            Directory.CreateDirectory(dir);
        }
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