using FolioDesk.Application.Abstractions;
using FolioDesk.Models;

namespace FolioDesk.Application;

public sealed class FolderQueryService(IFolioRepository repository, IFolioMutationLock mutationLock) {
    public FolioFolder? GetFolder(int folderId) {
        using var lease = mutationLock.Acquire();
        return repository.GetFolder(folderId);
    }
}
