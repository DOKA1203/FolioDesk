using FolioDesk.Application.Abstractions;

namespace FolioDesk.Application;

internal sealed class FolderIconCoordinator(
    IFolioRepository repository,
    IIconService iconService,
    IShortcutService shortcutService) {

    public string Refresh(int folderId) {
        var folder = repository.GetFolder(folderId) ??
                     throw new InvalidOperationException($"Folder {folderId} was not found.");
        var iconName = iconService.GenerateFolderIcon(folder);
        shortcutService.UpdateFolderShortcut(folderId, iconName);
        iconService.CleanupFolderIcons(folderId, iconName);
        return iconName;
    }
}
