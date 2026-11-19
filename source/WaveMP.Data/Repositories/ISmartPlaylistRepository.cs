using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// The smart playlists of the users and the tracks their rules pick. Every access to a smart playlist names the user;
/// smart playlists of other users are not found. The tracks are computed by the database on every call (see
/// <see cref="SmartPlaylistQueries"/>); ratings, favorites and plays are those of the user. Nothing is tracked.
/// </summary>
public interface ISmartPlaylistRepository
{
    Task<Page<SmartPlaylist>> ListAsync(PlaylistListQuery query, CancellationToken cancellationToken = default);

    Task<SmartPlaylist?> FindAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new smart playlist.
    /// </summary>
    /// <exception cref="UserNotFoundException">The user of the smart playlist does not exist.</exception>
    Task CreateAsync(SmartPlaylist playlist, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces name, rules, sort order and limit of a smart playlist of the user and sets its time of change.
    /// Returns <c>false</c> if it does not exist.
    /// </summary>
    Task<bool> UpdateAsync(SmartPlaylist playlist, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of the ids of the tracks the criteria pick for the user, in their order; the total respects the limit
    /// of the criteria.
    /// </summary>
    /// <param name="userId">The user whose ratings, favorites and plays the rules look at.</param>
    /// <param name="criteria">Rules, sort order and limit.</param>
    /// <param name="now">The time "in the last days" counts back from.</param>
    /// <param name="offset">Number of tracks to skip.</param>
    /// <param name="limit">Maximum number of tracks of the page.</param>
    /// <param name="cancellationToken">Aborts the query.</param>
    Task<Page<Guid>> ListTrackIdsAsync(
        Guid userId,
        SmartPlaylistCriteria criteria,
        DateTimeOffset now,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Number and total duration of the tracks the criteria pick for the user.
    /// </summary>
    Task<SmartPlaylistSummary> SummarizeAsync(
        Guid userId,
        SmartPlaylistCriteria criteria,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The first <paramref name="count"/> tracks the criteria pick, in their order, with whether they have a cover; as
    /// candidates of the cover of the smart playlist <paramref name="playlistId"/>.
    /// </summary>
    Task<IReadOnlyList<PlaylistCoverCandidateRow>> ListCoverCandidatesAsync(
        Guid playlistId,
        Guid userId,
        SmartPlaylistCriteria criteria,
        DateTimeOffset now,
        int count,
        CancellationToken cancellationToken = default);
}
