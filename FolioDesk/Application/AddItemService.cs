using System.IO;
using FolioDesk.Application.Abstractions;
using FolioDesk.Models;
using FolioDesk.Services;

namespace FolioDesk.Application;

public sealed class AddItemService(
    IFolioRepository repository,
    IIconService iconService,
    IShortcutService shortcutService,
    IItemFileStore fileStore,
    IFolioMutationLock mutationLock) {

    public FolioItem Add(int folderId, string sourcePath) {
        using var lease = mutationLock.Acquire();
        var folder = repository.GetFolder(folderId) ??
                     throw new InvalidOperationException($"Folder {folderId} was not found.");
        var itemName = GetUniqueItemName(folder, Path.GetFileNameWithoutExtension(sourcePath));
        StoredItemFile? storedItem = null;
        var dataSaved = false;

        try {
            storedItem = fileStore.Store(folderId, itemName, sourcePath);
            iconService.SaveItemIcon(storedItem.StoredPath, storedItem.IconPath);
            var item = new FolioItem {
                Icon = storedItem.IconPath,
                Name = itemName,
                Path = storedItem.StoredPath
            };
            repository.AddItem(folderId, item);
            dataSaved = true;
            new FolderIconCoordinator(repository, iconService, shortcutService).Refresh(folderId);
            AppLogger.Info($"Added application to folder. FolderId={folderId}, Name='{itemName}', Source='{sourcePath}', Stored='{storedItem.StoredPath}', MovedFromDesktop={storedItem.MovedFromDesktop}.");
            return item;
        }
        catch {
            if (dataSaved && storedItem is not null) {
                TryRollbackData(folderId, storedItem.StoredPath);
                TryRestoreFolderIcon(folderId);
            }
            if (storedItem is not null) fileStore.RollbackStore(storedItem);
            throw;
        }
    }

    private static string GetUniqueItemName(FolioFolder folder, string baseName) {
        var usedNames = folder.Files.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var item in folder.Files) {
            var storageDirectoryName = Path.GetFileName(Path.GetDirectoryName(item.Path));
            if (!string.IsNullOrWhiteSpace(storageDirectoryName)) usedNames.Add(storageDirectoryName);
        }

        var candidate = baseName;
        for (var counter = 2; usedNames.Contains(candidate); counter++)
            candidate = $"{baseName} ({counter})";
        return candidate;
    }

    private void TryRollbackData(int folderId, string storedPath) {
        try {
            repository.RemoveItem(folderId, storedPath);
            AppLogger.Info($"Rolled back added item data. FolderId={folderId}, Path='{storedPath}'.");
        }
        catch (Exception ex) {
            AppLogger.Error($"Failed to roll back added item data for folder {folderId}.", ex);
        }
    }

    private void TryRestoreFolderIcon(int folderId) {
        try {
            new FolderIconCoordinator(repository, iconService, shortcutService).Refresh(folderId);
        }
        catch (Exception ex) {
            AppLogger.Error($"Failed to restore the folder icon for {folderId} after rollback.", ex);
        }
    }
}
