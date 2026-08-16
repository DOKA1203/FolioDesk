using FolioDesk.Application.Abstractions;
using FolioDesk.Models;

namespace FolioDesk.Icons;

public sealed class WindowsIconService(string dataFolder) : IIconService {
    public void SaveItemIcon(string sourcePath, string pngPath) =>
        IconExtractor.SaveIconAsPng(sourcePath, pngPath);

    public string GenerateFolderIcon(FolioFolder folder) =>
        IconGenerator.GenerateIcon(folder, dataFolder);

    public void CleanupFolderIcons(int folderId, string iconNameToKeep) =>
        IconGenerator.CleanupFolderIcons(dataFolder, folderId, iconNameToKeep);
}
