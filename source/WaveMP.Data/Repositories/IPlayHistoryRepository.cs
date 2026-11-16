using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Access to the play history of the users. Nothing is tracked.
/// </summary>
public interface IPlayHistoryRepository
{
    /// <summary>
    /// Adds an entry. Returns <c>false</c> (and adds nothing) if its track does not exist. A deletion of the user or
    /// the track at the same time waits until the entry is saved and then removes it as well.
    /// </summary>
    /// <exception cref="UserNotFoundException">The user of the entry does not exist.</exception>
    Task<bool> AddAsync(PlayHistoryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the entries of a user, the newest play first.
    /// </summary>
    Task<Page<PlayHistoryEntry>> ListAsync(PlayHistoryListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an entry of the user; entries of other users are not found.
    /// </summary>
    Task<PlayHistoryEntry?> FindAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the tracks the user played, each once, the most recently played first (ties by id); the whole list holds
    /// at most <see cref="RecentTrackListQuery.MaxTracks"/> tracks.
    /// </summary>
    Task<Page<RecentTrackRow>> ListRecentTracksAsync(
        RecentTrackListQuery query,
        CancellationToken cancellationToken = default);
}
