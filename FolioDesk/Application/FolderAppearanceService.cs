using FolioDesk.Application.Abstractions;
using FolioDesk.Services;

namespace FolioDesk.Application;

public sealed class FolderAppearanceService(
    IFolioRepository repository,
    IIconService iconService,
    IShortcutService shortcutService,
    IFolioMutationLock mutationLock) {

    public string ChangeColor(int folderId, string argbHex) {
        using var lease = mutationLock.Acquire();
        var folder = repository.GetFolder(folderId) ??
                     throw new InvalidOperationException($"Folder {folderId} was not found.");
        var originalColor = folder.IconColor;
        repository.UpdateFolderColor(folderId, argbHex);
        try {
            return new FolderIconCoordinator(repository, iconService, shortcutService).Refresh(folderId);
        }
        catch {
            try {
                repository.UpdateFolderColor(folderId, originalColor);
                new FolderIconCoordinator(repository, iconService, shortcutService).Refresh(folderId);
            }
            catch (Exception rollbackException) {
                AppLogger.Error($"Failed to roll back folder color for {folderId}.", rollbackException);
            }
            throw;
        }
    }
}
