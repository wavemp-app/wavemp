using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Access to the connections of the users to scrobbling services and to the queue of scrobbles that wait to be sent.
/// Nothing is tracked.
/// </summary>
public interface IScrobbleRepository
{
    /// <summary>
    /// The connections of the user, at most one per service.
    /// </summary>
    Task<IReadOnlyList<ScrobbleConnection>> ListConnectionsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Number of queued scrobbles per connection of the user (by id of the connection); connections without queued
    /// scrobbles are missing.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountQueuedAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the connection of the user to its service. An existing connection to the same service keeps its id and
    /// its queued scrobbles and takes over account, credentials, status and time of the new one. Returns the stored
    /// connection.
    /// </summary>
    /// <exception cref="UserNotFoundException">The user does not exist.</exception>
    Task<ScrobbleConnection> SaveConnectionAsync(
        ScrobbleConnection connection,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the connection of the user to the service together with its queued scrobbles. Returns <c>false</c> if
    /// there is none.
    /// </summary>
    Task<bool> DeleteConnectionAsync(
        Guid userId,
        ScrobbleService service,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes the status of a connection; nothing happens if it no longer exists.
    /// </summary>
    Task SetConnectionStatusAsync(
        Guid connectionId,
        ScrobbleConnectionStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds scrobbles to the queue. Entries of connections that no longer exist are left out; a deletion of their
    /// connection at the same time waits until they are saved and then removes them as well. Returns the number of
    /// added entries.
    /// </summary>
    Task<int> EnqueueAsync(
        IReadOnlyCollection<ScrobbleQueueEntry> entries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Active connections that have queued scrobbles due at <paramref name="now"/>.
    /// </summary>
    Task<IReadOnlyList<ScrobbleConnection>> ListDueConnectionsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The queued scrobbles of the connection that are due at <paramref name="now"/>, the first played first.
    /// </summary>
    Task<IReadOnlyList<ScrobbleQueueEntry>> ListDueEntriesAsync(
        Guid connectionId,
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes sent (or rejected) scrobbles of the connection from the queue and notes when the connection scrobbled
    /// the last time, if <paramref name="scrobbledAt"/> is set.
    /// </summary>
    Task RemoveEntriesAsync(
        Guid connectionId,
        IReadOnlyCollection<Guid> entryIds,
        DateTimeOffset? scrobbledAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts a failed attempt for the scrobbles of the connection that are due at <paramref name="now"/> and sends
    /// them again not before <paramref name="nextAttemptAt"/>.
    /// </summary>
    Task PostponeDueEntriesAsync(
        Guid connectionId,
        DateTimeOffset now,
        DateTimeOffset nextAttemptAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the scrobbles that were played before <paramref name="playedBefore"/>, which the services no longer
    /// accept. Returns their number.
    /// </summary>
    Task<int> RemoveExpiredAsync(DateTimeOffset playedBefore, CancellationToken cancellationToken = default);

    /// <summary>
    /// The earliest time a queued scrobble of an active connection is due, or <c>null</c> if there is none.
    /// </summary>
    Task<DateTimeOffset?> NextAttemptAtAsync(CancellationToken cancellationToken = default);
}
