using FolioDesk.Application;
using FolioDesk.Application.Abstractions;
using FolioDesk.Icons;
using FolioDesk.Infrastructure.Concurrency;
using FolioDesk.Infrastructure.Files;
using FolioDesk.Infrastructure.Persistence;
using FolioDesk.ShortCuts;

namespace FolioDesk;

internal sealed class AppComposition(string dataFolder, string executablePath) {
    public FolderQueryService CreateFolderQueryService() => new(CreateRepository(), CreateMutationLock());

    public CreateFolderService CreateFolderService() => new(
        CreateRepository(),
        CreateIconService(),
        CreateShortcutService(),
        CreateFileStore(),
        CreateMutationLock(),
        executablePath);

    public AddItemService CreateAddItemService() => new(
        CreateRepository(),
        CreateIconService(),
        CreateShortcutService(),
        CreateFileStore(),
        CreateMutationLock());

    public FolderContentService CreateFolderContentService() => new(
        CreateRepository(),
        CreateIconService(),
        CreateShortcutService(),
        CreateFileStore(),
        CreateMutationLock());

    public FolderAppearanceService CreateFolderAppearanceService() => new(
        CreateRepository(),
        CreateIconService(),
        CreateShortcutService(),
        CreateMutationLock());

    private IFolioRepository CreateRepository() => new JsonFolioRepository(dataFolder);
    private IIconService CreateIconService() => new WindowsIconService(dataFolder);
    private IShortcutService CreateShortcutService() => new WindowsShortcutService(dataFolder);
    private IItemFileStore CreateFileStore() => new LocalItemFileStore(dataFolder);
    private static IFolioMutationLock CreateMutationLock() => new NamedMutexFolioMutationLock();
}
