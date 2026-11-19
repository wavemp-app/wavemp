using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Playlists.SmartPlaylists;

/// <summary>
/// The smart playlists of a user: rules that pick tracks from the library. Their tracks are computed on every request,
/// so they always reflect the current library and the ratings, favorites and plays of the user. Every method names the
/// user; smart playlists of other users are not found.
/// </summary>
public interface ISmartPlaylistService
{
    /// <summary>
    /// Lists the smart playlists of the user of the query with what they hold right now and their covers.
    /// </summary>
    Task<Page<SmartPlaylistItem>> ListAsync(PlaylistListQuery query, CancellationToken cancellationToken = default);

    Task<SmartPlaylistItem?> GetAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks and stores a new smart playlist.
    /// </summary>
    Task<SmartPlaylistResult> CreateAsync(
        Guid userId,
        SmartPlaylistDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks and replaces name, rules, sort order and limit of a smart playlist.
    /// </summary>
    Task<SmartPlaylistResult> UpdateAsync(
        Guid userId,
        Guid id,
        SmartPlaylistDefinition definition,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a smart playlist; the tracks stay in the library. Returns <c>false</c> if it does not exist.
    /// </summary>
    Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of the tracks of a smart playlist in its order, computed now, or <c>null</c> if the smart playlist does
    /// not exist.
    /// </summary>
    Task<Page<TrackRow>?> ListTracksAsync(
        Guid userId,
        Guid id,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a smart playlist into a normal playlist with the same name and its tracks of right now, in their
    /// order; the smart playlist is deleted.
    /// </summary>
    Task<SmartPlaylistConversion> ConvertAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
}
