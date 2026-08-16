using FolioDesk.Models;

namespace FolioDesk.Application.Abstractions;

public interface IFolioRepository {
    FolioFolder? GetFolder(int folderId);
    FolioFolder CreateFolder(string name);
    void DeleteFolder(int folderId);
    void AddItem(int folderId, FolioItem item);
    void RemoveItem(int folderId, string itemPath);
    void ReorderItems(int folderId, IReadOnlyList<string> orderedPaths);
    void UpdateFolderColor(int folderId, string argbHex);
}
