using FolioDesk.Application.Abstractions;
using FolioDesk.Models;
using FolioDesk.Services;

namespace FolioDesk.Application;

public sealed class FolderContentService(
    IFolioRepository repository,
    IIconService iconService,
    IShortcutService shortcutService,
    IItemFileStore fileStore,
    IFolioMutationLock mutationLock) {

    public string ExtractToDesktop(int folderId, FolioItem item) {
        using var lease = mutationLock.Acquire();
        var originalFolder = repository.GetFolder(folderId) ??
                             throw new InvalidOperationException($"Folder {folderId} was not found.");
        var originalOrder = originalFolder.Files.OrderBy(file => file.Order).Select(file => file.Path).ToList();
        ExtractedItemFile? extractedItem = null;
        var dataSaved = false;

        try {
            extractedItem = fileStore.MoveToDesktop(item.Path);
            repository.RemoveItem(folderId, item.Path);
            dataSaved = true;
            new FolderIconCoordinator(repository, iconService, shortcutService).Refresh(folderId);
            fileStore.CompleteExtraction(extractedItem);
            AppLogger.Info($"Extracted item from folder to desktop. FolderId={folderId}, Name='{item.Name}', Destination='{extractedItem.DesktopPath}'.");
            return extractedItem.DesktopPath;
        }
        catch {
            if (dataSaved) TryRestoreItem(folderId, item, originalOrder);
            if (extractedItem is not null) fileStore.RollbackExtraction(extractedItem);
            throw;
        }
    }

    public void Reorder(int folderId, IReadOnlyList<FolioItem> orderedItems) {
        using var lease = mutationLock.Acquire();
        var originalFolder = repository.GetFolder(folderId) ??
                             throw new InvalidOperationException($"Folder {folderId} was not found.");
        var originalOrder = originalFolder.Files.OrderBy(item => item.Order).Select(item => item.Path).ToList();
        repository.ReorderItems(folderId, orderedItems.Select(item => item.Path).ToList());
        try {
            new FolderIconCoordinator(repository, iconService, shortcutService).Refresh(folderId);
        }
        catch {
            try {
                repository.ReorderItems(folderId, originalOrder);
                new FolderIconCoordinator(repository, iconService, shortcutService).Refresh(folderId);
            }
            catch (Exception rollbackException) {
                AppLogger.Error($"Failed to roll back item order for folder {folderId}.", rollbackException);
            }
            throw;
        }
    }

    private void TryRestoreItem(int folderId, FolioItem item, IReadOnlyList<string> originalOrder) {
        try {
            repository.AddItem(folderId, item);
            repository.ReorderItems(folderId, originalOrder);
            new FolderIconCoordinator(repository, iconService, shortcutService).Refresh(folderId);
        }
        catch (Exception ex) {
            AppLogger.Error($"Failed to restore extracted item data for folder {folderId}.", ex);
        }
    }
}
