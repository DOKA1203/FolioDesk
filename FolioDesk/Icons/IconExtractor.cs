namespace FolioDesk.Icons;

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using IWshRuntimeLibrary;
using Microsoft.Win32;

/// <summary>
/// 파일의 셸 아이콘을 추출합니다.
/// 크기 우선순위: JUMBO(256) → EXTRALARGE(48) → LARGE(32)
/// .lnk 바로가기 및 UWP/스토어 앱 아이콘을 지원합니다.
/// </summary>
public static class IconExtractor
{
    #region Win32 / COM Interop

    [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
    private static extern int SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        out SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("Shell32.dll")]
    private static extern int SHGetImageList(
        int iImageList,
        ref Guid riid,
        out IImageList ppv);

    [DllImport("User32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
    private static extern int ExtractIconEx(
        string lpszFile,
        int nIconIndex,
        [Out] IntPtr[] phiconLarge,
        [Out] IntPtr[] phiconSmall,
        int nIcons);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;           // 시스템 이미지 리스트 인덱스
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    // SHGetFileInfo flags
    private const uint SHGFI_ICON        = 0x00000100;
    private const uint SHGFI_LARGEICON   = 0x00000000;
    private const uint SHGFI_SYSICONINDEX = 0x00004000; // 이미지 리스트 인덱스 반환

    // Shell Image List 크기
    private const int SHIL_LARGE      = 0; // 32×32
    private const int SHIL_EXTRALARGE = 2; // 48×48
    private const int SHIL_JUMBO      = 4; // 256×256

    private const uint ILD_TRANSPARENT = 0x00000001;

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(IntPtr himlImage, IntPtr himlMask, out int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, out int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr himlImage, IntPtr himlMask);
        [PreserveSig] int AddMasked(IntPtr himlImage, int crMask, out int pi);
        [PreserveSig] int Draw(ref IMAGELISTDRAWPARAMS pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, uint flags, out IntPtr picon);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IMAGELISTDRAWPARAMS
    {
        public int cbSize;
        public IntPtr himl;
        public int i;
        public IntPtr hdcDst;
        public int x, y, cx, cy, xBitmap, yBitmap;
        public int rgbBk, rgbFg;
        public uint fStyle, dwRop;
        public uint fState;
        public uint Frame;
        public uint crEffect;
    }

    private static readonly Guid IID_IImageList =
        new("46EB5926-582E-4017-9FDF-E8998DAA0950");

    #endregion

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// 지정된 파일의 아이콘을 BitmapSource로 추출합니다.
    /// 크기 우선순위: JUMBO(256) → EXTRALARGE(48) → LARGE(32)
    /// </summary>
    /// <exception cref="ArgumentException">filePath가 null 또는 비어있는 경우</exception>
    /// <exception cref="FileNotFoundException">파일을 찾을 수 없는 경우</exception>
    /// <exception cref="InvalidOperationException">아이콘 추출에 실패한 경우</exception>
    public static BitmapSource GetIcon(string filePath)
    {
        ValidatePath(filePath);

        IconSource source = ResolvePath(filePath);

        BitmapSource bitmap = TryExtractWithFallback(source)
            ?? throw new InvalidOperationException($"아이콘 추출 실패: {filePath}");

        return EnsureSize(bitmap, 64);
    }

    /// <summary>
    /// GetIcon의 비동기 버전입니다. 백그라운드 스레드에서 아이콘을 추출합니다.
    /// </summary>
    public static Task<BitmapSource> GetIconAsync(string filePath)
        => Task.Run(() => GetIcon(filePath));

    /// <summary>
    /// 아이콘을 PNG 파일로 저장합니다.
    /// </summary>
    /// <exception cref="ArgumentException">savePath가 null 또는 비어있는 경우</exception>
    public static void SaveIconAsPng(string filePath, string savePath)
    {
        if (string.IsNullOrWhiteSpace(savePath))
            throw new ArgumentException("저장 경로가 유효하지 않습니다.", nameof(savePath));

        var source = GetIcon(filePath);
        WritePng(source, savePath);
    }

    /// <summary>
    /// SaveIconAsPng의 비동기 버전입니다.
    /// </summary>
    public static Task SaveIconAsPngAsync(string filePath, string savePath)
        => Task.Run(() => SaveIconAsPng(filePath, savePath));

    // -------------------------------------------------------------------------
    // Path Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// 아이콘 추출에 필요한 파일 경로와 아이콘 인덱스를 묶은 컨텍스트입니다.
    /// IconIndex = -1 이면 파일의 기본 아이콘(인덱스 0)을 사용합니다.
    /// </summary>
    private readonly record struct IconSource(string FilePath, int IconIndex = -1);

    private static void ValidatePath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("파일 경로가 유효하지 않습니다.", nameof(filePath));

        if (!System.IO.File.Exists(filePath))
            throw new FileNotFoundException("파일을 찾을 수 없습니다.", filePath);
    }

    /// <summary>
    /// .lnk 바로가기를 분석해 실제 아이콘 소스(경로 + 인덱스)를 반환합니다.
    /// 우선순위: IconLocation → TargetPath → UWP 매니페스트 → 원본 .lnk
    /// </summary>
    private static IconSource ResolvePath(string filePath)
    {
        if (!filePath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            return new IconSource(filePath);

        try
        {
            var shell = new WshShell();
            var shortcut = (IWshShortcut)shell.CreateShortcut(filePath);
            try
            {
                // 1순위: IconLocation — 런처 기반 앱(예: 발로란트)은 여기에
                //         실제 아이콘 경로와 인덱스가 명시됩니다.
                string iconLocation = shortcut.IconLocation;
                if (!string.IsNullOrWhiteSpace(iconLocation) && iconLocation != ",0")
                {
                    var (iconPath, iconIndex) = ParseIconLocation(iconLocation);
                    if (System.IO.File.Exists(iconPath))
                        return new IconSource(iconPath, iconIndex);
                }

                // 2순위: TargetPath
                string target = shortcut.TargetPath;
                if (!string.IsNullOrWhiteSpace(target) && System.IO.File.Exists(target))
                    return new IconSource(target);

                // 3순위: UWP 패키지 매니페스트
                string? uwpIconPath = TryResolveUwpIconPath(filePath);
                if (uwpIconPath != null)
                    return new IconSource(uwpIconPath);
            }
            finally
            {
                Marshal.FinalReleaseComObject(shortcut);
                Marshal.FinalReleaseComObject(shell);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"바로가기 분석 실패: {ex.Message}");
        }

        return new IconSource(filePath);
    }

    /// <summary>
    /// "C:\path\to\file.exe,2" 형식의 IconLocation을 경로와 인덱스로 분리합니다.
    /// </summary>
    private static (string path, int index) ParseIconLocation(string iconLocation) {
        var commaPos = iconLocation.LastIndexOf(',');
        if (commaPos < 0)
            return (iconLocation.Trim(), 0);

        var path  = iconLocation[..commaPos].Trim();
        var indexStr = iconLocation[(commaPos + 1)..].Trim();

        var index = int.TryParse(indexStr, out var parsed) ? Math.Max(parsed, 0) : 0;
        return (path, index);
    }

    // -------------------------------------------------------------------------
    // UWP / Store App Icon Resolution
    // -------------------------------------------------------------------------

    /// <summary>
    /// UWP 앱 바로가기(.lnk)에서 패키지 이름을 추출하고,
    /// AppxManifest.xml을 파싱해 가장 큰 아이콘 파일 경로를 반환합니다.
    /// </summary>
    private static string? TryResolveUwpIconPath(string lnkPath)
    {
        try
        {
            // 바로가기 파일명에서 패키지 Family Name 힌트를 얻을 수 없으므로
            // 레지스트리의 패키지 목록을 순회합니다.
            var packageFamilyName = FindPackageFamilyNameFromLnk(lnkPath);
            if (packageFamilyName == null) return null;
            return FindBestUwpIconPath(packageFamilyName);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"UWP 아이콘 해석 실패: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 레지스트리에서 패키지 설치 경로를 찾아 AppxManifest.xml을 파싱합니다.
    /// 바로가기 파일명이 앱 이름과 일치하는 패키지를 우선합니다.
    /// </summary>
    private static string? FindPackageFamilyNameFromLnk(string lnkPath)
    {
        const string packagesKey =
            @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

        using RegistryKey? root = Registry.CurrentUser.OpenSubKey(packagesKey);
        if (root == null)
            return null;

        string lnkName = Path.GetFileNameWithoutExtension(lnkPath);

        foreach (string packageName in root.GetSubKeyNames())
        {
            using RegistryKey? pkg = root.OpenSubKey(packageName);
            string? installPath = pkg?.GetValue("PackageRootFolder") as string;
            if (string.IsNullOrEmpty(installPath))
                continue;

            string manifestPath = Path.Combine(installPath, "AppxManifest.xml");
            if (!System.IO.File.Exists(manifestPath))
                continue;

            // 바로가기 이름이 앱 DisplayName과 대략 일치하는지 확인
            if (ManifestMatchesLnkName(manifestPath, lnkName))
                return packageName;
        }

        return null;
    }

    /// <summary>
    /// AppxManifest.xml의 DisplayName이 바로가기 이름과 일치하는지 확인합니다.
    /// </summary>
    private static bool ManifestMatchesLnkName(string manifestPath, string lnkName) {
        try {
            var doc = XDocument.Load(manifestPath);
            // XNamespace ns = "http://schemas.microsoft.com/appx/manifest/foundation/windows10";
            // XNamespace uap = "http://schemas.microsoft.com/appx/manifest/uap/windows10";

            // DisplayName은 Properties 또는 Application/VisualElements 에 있음
            var displayNames = doc.Descendants()
                .Where(e => e.Name.LocalName == "DisplayName")
                .Select(e => e.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v) && !v.StartsWith("ms-resource:"));

            return displayNames.Any(name =>
                name.Contains(lnkName, StringComparison.OrdinalIgnoreCase) ||
                lnkName.Contains(name, StringComparison.OrdinalIgnoreCase));
        }
        catch {
            return false;
        }
    }

    /// <summary>
    /// 패키지 Family Name으로 설치 경로를 찾고, AppxManifest에서 아이콘 경로를 읽어
    /// 실제로 존재하는 가장 큰 이미지 파일을 반환합니다.
    /// </summary>
    private static string? FindBestUwpIconPath(string packageName) {
        const string packagesKey =
            @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

        using RegistryKey? root = Registry.CurrentUser.OpenSubKey(packagesKey);
        using RegistryKey? pkg = root?.OpenSubKey(packageName);

        string? installPath = pkg?.GetValue("PackageRootFolder") as string;
        if (string.IsNullOrEmpty(installPath))
            return null;

        string manifestPath = Path.Combine(installPath, "AppxManifest.xml");
        if (!System.IO.File.Exists(manifestPath))
            return null;

        string? relativeIconPath = ParseIconPathFromManifest(manifestPath);
        if (relativeIconPath == null)
            return null;

        // 예: Assets\Square44x44Logo.png → scale-400(256px) 버전을 우선 탐색
        return FindBestScaledIcon(installPath, relativeIconPath);
    }

    /// <summary>
    /// AppxManifest.xml에서 Square 계열 아이콘 경로를 추출합니다.
    /// 큰 크기(Square310, Square150, Square44) 순으로 우선합니다.
    /// </summary>
    private static string? ParseIconPathFromManifest(string manifestPath) {
        try {
            var doc = XDocument.Load(manifestPath);

            // 우선순위: Square310x310 → Square150x150 → Square44x44 → Logo
            string[] preferredAttributes = [ "Square310x310Logo", "Square150x150Logo", "Square44x44Logo", "Logo" ];

            foreach (var attr in preferredAttributes) {
                var value = doc.Descendants()
                    .Where(e => e.Attribute(attr) != null)
                    .Select(e => e.Attribute(attr)!.Value)
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(value))
                    return value;
            }
        }
        catch (Exception ex) {
            Debug.WriteLine($"Manifest 파싱 실패: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Windows 스케일 접미사(scale-400 → scale-200 → scale-100)를 순서대로 탐색해
    /// 실제 존재하는 가장 큰 이미지 파일을 반환합니다.
    /// </summary>
    private static string? FindBestScaledIcon(string installPath, string relativeIconPath) {
        var dir = Path.GetDirectoryName(relativeIconPath) ?? "";
        var nameWithoutExt = Path.GetFileNameWithoutExtension(relativeIconPath);
        var ext = Path.GetExtension(relativeIconPath);

        // scale-400 = 256px, scale-200 = 96px, scale-100 = 48px
        string[] scales = [ "scale-400", "scale-200", "scale-150", "scale-100" ];

        foreach (var scale in scales)
        {
            var candidate = Path.Combine(
                installPath, dir,
                $"{nameWithoutExt}.{scale}{ext}");

            if (System.IO.File.Exists(candidate))
                return candidate;
        }

        // 스케일 접미사 없는 원본
        var plain = Path.Combine(installPath, relativeIconPath);
        return System.IO.File.Exists(plain) ? plain : null;
    }

    // -------------------------------------------------------------------------
    // Extraction Chain
    // -------------------------------------------------------------------------

    /// <summary>
    /// JUMBO → EXTRALARGE → LARGE 순서로 아이콘 추출을 시도합니다.
    /// UWP 아이콘처럼 PNG 파일이 직접 반환된 경우 BitmapSource로 바로 로드합니다.
    /// </summary>
    private static BitmapSource? TryExtractWithFallback(IconSource source) {
        // UWP 아이콘은 PNG 파일로 직접 반환됨 → BitmapImage로 로드
        string ext = Path.GetExtension(source.FilePath);
        if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)) {
            return TryLoadBitmapFromFile(source.FilePath);
        }

        // IconLocation에서 명시적 인덱스를 받은 경우 그 인덱스로 직접 추출
        // 그렇지 않으면 SHGetFileInfo로 시스템 이미지 리스트 인덱스를 조회
        int iconIndex = source.IconIndex >= 0
            ? GetSystemIconIndexByFileIndex(source.FilePath, source.IconIndex)
            : GetSystemIconIndex(source.FilePath);

        if (iconIndex >= 0)
        {
            foreach (int size in new[] { SHIL_JUMBO, SHIL_EXTRALARGE, SHIL_LARGE })
            {
                var bitmap = TryGetIconFromImageList(iconIndex, size);
                if (bitmap != null)
                {
                    Debug.WriteLine($"아이콘 추출 성공 (SHIL={size}): {source.FilePath}");
                    return bitmap;
                }
            }
        }

        // 최후 수단: GDI Icon.ExtractAssociatedIcon
        return TryGetAssociatedIcon(source.FilePath);
    }

    /// <summary>
    /// SHGetFileInfo로 시스템 이미지 리스트 인덱스(iIcon)를 가져옵니다. (기본 아이콘)
    /// </summary>
    private static int GetSystemIconIndex(string filePath) {
        int result = SHGetFileInfo(
            filePath,
            0,
            out SHFILEINFO shinfo,
            (uint)Marshal.SizeOf<SHFILEINFO>(),
            SHGFI_SYSICONINDEX | SHGFI_LARGEICON);

        if (result == 0) {
            Debug.WriteLine($"SHGetFileInfo(SYSICONINDEX) 실패: {filePath}");
            return -1;
        }

        return shinfo.iIcon;
    }

    /// <summary>
    /// IconLocation에 명시된 파일 내 아이콘 인덱스(fileIconIndex)를
    /// 시스템 이미지 리스트 인덱스로 변환합니다.
    /// ExtractIconEx로 핸들을 얻은 뒤 SHGetFileInfo로 매핑합니다.
    /// </summary>
    private static int GetSystemIconIndexByFileIndex(string filePath, int fileIconIndex) {
        // fileIconIndex가 0이면 일반 경로와 동일하게 처리
        if (fileIconIndex == 0)
            return GetSystemIconIndex(filePath);

        // ExtractIconEx로 특정 인덱스의 아이콘 핸들을 얻어
        // 시스템 이미지 리스트 인덱스로 변환합니다.
        // 변환이 실패하면 기본 인덱스로 폴백합니다.
        try {
            IntPtr[] largeIcons = new IntPtr[1];
            int extracted = ExtractIconEx(filePath, fileIconIndex, largeIcons, null!, 1);
            if (extracted <= 0 || largeIcons[0] == IntPtr.Zero)
                return GetSystemIconIndex(filePath);

            try {
                // hIcon → 시스템 이미지 리스트 인덱스 변환
                int result = SHGetFileInfo(
                    filePath,
                    0,
                    out SHFILEINFO shinfo,
                    (uint)Marshal.SizeOf<SHFILEINFO>(),
                    SHGFI_SYSICONINDEX | SHGFI_LARGEICON);

                // SHGetFileInfo는 파일 기준으로 인덱스를 반환하므로
                // fileIconIndex를 직접 오프셋으로 더합니다.
                return result != 0 ? shinfo.iIcon + fileIconIndex : -1;
            }
            finally {
                DestroyIcon(largeIcons[0]);
            }
        }
        catch (Exception ex) {
            Debug.WriteLine($"GetSystemIconIndexByFileIndex 실패: {ex.Message}");
            return GetSystemIconIndex(filePath);
        }
    }

    /// <summary>
    /// 지정한 크기의 Shell Image List에서 아이콘을 추출합니다.
    /// </summary>
    private static BitmapSource? TryGetIconFromImageList(int iconIndex, int shilSize) {
        try {
            var iid = IID_IImageList;
            int hr = SHGetImageList(shilSize, ref iid, out IImageList imageList);
            if (hr != 0)
                return null;

            try {
                hr = imageList.GetIcon(iconIndex, ILD_TRANSPARENT, out IntPtr hIcon);
                if (hr != 0 || hIcon == IntPtr.Zero)
                    return null;

                try
                {
                    return CreateFrozenBitmapSource(hIcon);
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
            finally {
                Marshal.FinalReleaseComObject(imageList);
            }
        }
        catch (Exception ex) {
            Debug.WriteLine($"ImageList(SHIL={shilSize}) 추출 실패: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// GDI Icon.ExtractAssociatedIcon을 통한 최후 수단 추출.
    /// </summary>
    private static BitmapSource? TryGetAssociatedIcon(string filePath) {
        try {
            using Icon? icon = Icon.ExtractAssociatedIcon(filePath);
            if (icon == null) return null;

            return CreateFrozenBitmapSource(icon.Handle);
        }
        catch (Exception ex) {
            Debug.WriteLine($"ExtractAssociatedIcon 실패: {ex.Message}");
            return null;
        }
    }

    // -------------------------------------------------------------------------
    // BitmapSource Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// PNG/JPG 파일을 BitmapSource로 직접 로드합니다. (UWP 아이콘용)
    /// </summary>
    private static BitmapSource? TryLoadBitmapFromFile(string imagePath) {
        try {
            var image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(imagePath, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();

            if (image.CanFreeze)
                image.Freeze();

            return image;
        }
        catch (Exception ex) {
            Debug.WriteLine($"이미지 파일 로드 실패: {ex.Message}");
            return null;
        }
    }

    private static BitmapSource CreateFrozenBitmapSource(IntPtr hIcon) {
        var source = Imaging.CreateBitmapSourceFromHIcon(
            hIcon,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());

        if (source.CanFreeze)
            source.Freeze();

        return source;
    }

    /// <summary>
    /// 이미지를 목표 크기(정사각형)로 리사이즈합니다.
    /// 이미 목표 크기와 같으면 원본을 그대로 반환합니다.
    /// 업스케일/다운스케일 모두 Fant 알고리즘을 사용합니다.
    /// </summary>
    private static BitmapSource EnsureSize(BitmapSource source, int targetSize) {
        if (source.PixelWidth == targetSize && source.PixelHeight == targetSize)
            return source;

        var scaled = new TransformedBitmap(
            source,
            new System.Windows.Media.ScaleTransform(
                (double)targetSize / source.PixelWidth,
                (double)targetSize / source.PixelHeight));

        // FormatConvertedBitmap으로 픽셀 포맷을 Pbgra32로 고정합니다.
        // PNG 저장 및 WPF 바인딩에서 포맷 불일치를 방지합니다.
        var converted = new FormatConvertedBitmap(
            scaled,
            System.Windows.Media.PixelFormats.Pbgra32,
            null,
            0);

        if (converted.CanFreeze)
            converted.Freeze();

        return converted;
    }

    // -------------------------------------------------------------------------
    // PNG Save
    // -------------------------------------------------------------------------

    private static void WritePng(BitmapSource source, string savePath) {
        var directory = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));

        using var stream = System.IO.File.Create(savePath);
        encoder.Save(stream);
    }
}