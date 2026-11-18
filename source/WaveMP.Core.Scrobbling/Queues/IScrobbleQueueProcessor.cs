namespace WaveMP.Core.Scrobbling.Queues;

/// <summary>
/// Sends the due scrobbles of the queue to the services, in batches per connection and in the order they were played.
/// Sent scrobbles leave the queue; when a service cannot be reached, its scrobbles wait longer after every failed
/// attempt; when it rejects the credentials, the connection waits for the user to connect it again.
/// </summary>
public interface IScrobbleQueueProcessor
{
    /// <summary>
    /// Sends what is due now and drops scrobbles the services no longer accept. Returns when the next scrobble is
    /// due, or <c>null</c> if nothing waits.
    /// </summary>
    Task<DateTimeOffset?> ProcessAsync(CancellationToken cancellationToken = default);
}
