using FolioDesk.Application.Abstractions;
using FolioDesk.Models;
using FolioDesk.Services;

namespace FolioDesk.Application;

public sealed class CreateFolderService(
    IFolioRepository repository,
    IIconService iconService,
    IShortcutService shortcutService,
    IItemFileStore fileStore,
    IFolioMutationLock mutationLock,
    string executablePath) {

    public FolioFolder Create(string folderName, string shortcutNameTemplate) {
        using var lease = mutationLock.Acquire();
        var folder = repository.CreateFolder(folderName);
        try {
            var iconName = iconService.GenerateFolderIcon(folder);
            var shortcutName = string.Format(shortcutNameTemplate, folder.Id);
            shortcutService.CreateFolderShortcut(executablePath, folder.Id, shortcutName, iconName);
            iconService.CleanupFolderIcons(folder.Id, iconName);
            return folder;
        }
        catch {
            try {
                repository.DeleteFolder(folder.Id);
                fileStore.DeleteFolderStorage(folder.Id);
                AppLogger.Info($"Rolled back folder creation. FolderId={folder.Id}.");
            }
            catch (Exception rollbackException) {
                AppLogger.Error($"Failed to roll back folder creation for {folder.Id}.", rollbackException);
            }
            throw;
        }
    }
}
