namespace WaveMP.Core.Scrobbling.Queues;

/// <summary>
/// Wakes the sender of the queue when there is something new to send: a new play or a connection that works again.
/// </summary>
public sealed class ScrobbleQueueSignal : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(0, 1);

    public void Notify()
    {
        try
        {
            _semaphore.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already signaled.
        }
    }

    /// <summary>
    /// Waits for the next signal, at most <paramref name="timeout"/>. Returns <c>true</c> if signaled.
    /// </summary>
    public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        return _semaphore.WaitAsync(timeout, cancellationToken);
    }

    public void Dispose()
    {
        _semaphore.Dispose();
    }
}
