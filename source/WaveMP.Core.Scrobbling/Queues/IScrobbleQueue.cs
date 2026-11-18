using WaveMP.Data.Projections;

namespace WaveMP.Core.Scrobbling.Queues;

/// <summary>
/// The queue of scrobbles in the database: every play that counts is queued for each connected service of the user
/// and sent in the background (see <see cref="IScrobbleQueueProcessor"/>), again and again until the service takes it.
/// </summary>
public interface IScrobbleQueue
{
    /// <summary>
    /// Queues a play of the track for every service the user connected and wakes the sender. Nothing is queued for
    /// songs shorter than 30 seconds, songs without artist and plays the services no longer accept. Returns the number
    /// of queued scrobbles.
    /// </summary>
    /// <param name="userId">The user who played the track.</param>
    /// <param name="track">The track as it was played.</param>
    /// <param name="playedAt">When the playback started; the time of the scrobble.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    Task<int> EnqueueAsync(
        Guid userId,
        TrackRow track,
        DateTimeOffset playedAt,
        CancellationToken cancellationToken = default);
}
