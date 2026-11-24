namespace WaveMP.Core.Scanning.Writers;

/// <summary>
/// Lets only one long maintenance run at a time work on the media folders: a scan holds it for its whole run (the
/// regrouping included), the organization of the media files for the whole run of an operation and for the recovery
/// of an interrupted one on start. So a scan started meanwhile (e.g. by the file watcher, which sees the moves) waits
/// and then finds disk and library in agreement, and an organize run waits for a running scan. Short writers (a folder
/// read again, the metadata editor) only take the <see cref="LibraryWriteLock"/>. Whoever needs both takes this lock
/// first. Not reentrant.
/// </summary>
public sealed class LibraryMaintenanceLock : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    /// <summary>
    /// Whether a run holds the lock right now.
    /// </summary>
    public bool IsHeld => _semaphore.CurrentCount == 0;

    /// <summary>
    /// Waits until no other run holds the lock. Disposing the result releases it.
    /// </summary>
    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new Releaser(_semaphore);
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }

    /// <summary>
    /// Releases the lock once, however often it is disposed.
    /// </summary>
    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}
