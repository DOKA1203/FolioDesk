namespace FolioDesk.Application.Abstractions;

public interface IFolioMutationLock {
    IDisposable Acquire();
}
