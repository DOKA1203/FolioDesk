using FolioDesk.Application.Abstractions;

namespace FolioDesk.Infrastructure.Concurrency;

public sealed class NamedMutexFolioMutationLock : IFolioMutationLock {
    private const string MutexName = @"Local\FolioDesk.FolioData.Mutation";

    public IDisposable Acquire() {
        var mutex = new Mutex(initiallyOwned: false, MutexName);
        try {
            try {
                mutex.WaitOne();
            }
            catch (AbandonedMutexException) {
                // An abandoned mutex is already acquired by the current thread.
            }
            return new Releaser(mutex);
        }
        catch {
            mutex.Dispose();
            throw;
        }
    }

    private sealed class Releaser(Mutex mutex) : IDisposable {
        private Mutex? _mutex = mutex;

        public void Dispose() {
            var mutexToRelease = Interlocked.Exchange(ref _mutex, null);
            if (mutexToRelease is null) return;
            mutexToRelease.ReleaseMutex();
            mutexToRelease.Dispose();
        }
    }
}
