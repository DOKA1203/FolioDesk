namespace FolioDesk.Application.Abstractions;

public interface IShortcutService {
    void CreateFolderShortcut(string targetPath, int folderId, string shortcutName, string iconName);
    void UpdateFolderShortcut(int folderId, string iconName);
}
