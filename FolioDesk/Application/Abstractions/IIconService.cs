using FolioDesk.Models;

namespace FolioDesk.Application.Abstractions;

public interface IIconService {
    void SaveItemIcon(string sourcePath, string pngPath);
    string GenerateFolderIcon(FolioFolder folder);
    void CleanupFolderIcons(int folderId, string iconNameToKeep);
}
