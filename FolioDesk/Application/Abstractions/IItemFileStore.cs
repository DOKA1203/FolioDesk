namespace FolioDesk.Application.Abstractions;

public sealed record StoredItemFile(
    string SourcePath,
    string StoredPath,
    string IconPath,
    string ItemDirectory,
    bool MovedFromDesktop);

public sealed record ExtractedItemFile(
    string StoredPath,
    string DesktopPath,
    string ItemDirectory);

public interface IItemFileStore {
    StoredItemFile Store(int folderId, string itemName, string sourcePath);
    void RollbackStore(StoredItemFile storedItem);
    ExtractedItemFile MoveToDesktop(string storedPath);
    void RollbackExtraction(ExtractedItemFile extractedItem);
    void CompleteExtraction(ExtractedItemFile extractedItem);
    void DeleteFolderStorage(int folderId);
}
