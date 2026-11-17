using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Data access of the lyrics: the files of a track that carry lyrics and the results of the online lookup.
/// </summary>
public interface ILyricsRepository
{
    /// <summary>
    /// The track with its lyrics file, its primary files and the result of its last lookup, or <c>null</c> if the
    /// track does not exist.
    /// </summary>
    Task<LyricsTrackRow?> FindTrackAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks that were never looked up online and have no lyrics file, the ones found first first.
    /// </summary>
    /// <param name="limit">Maximum number of tracks.</param>
    /// <param name="excluded">Tracks to leave out, e.g. those a running job already handled.</param>
    /// <param name="cancellationToken">Aborts the query.</param>
    Task<IReadOnlyList<LyricsTrackRow>> ListTracksToLookUpAsync(
        int limit,
        IReadOnlyCollection<Guid> excluded,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the result of a lookup and replaces an earlier one of the track. Returns <c>false</c> if the track no
    /// longer exists.
    /// </summary>
    Task<bool> SaveDownloadAsync(LyricsDownload download, CancellationToken cancellationToken = default);
}
