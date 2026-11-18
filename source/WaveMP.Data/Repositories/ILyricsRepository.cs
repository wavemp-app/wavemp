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
    /// Tracks without lyrics file to look up online: first those that were never looked up, then those whose last
    /// lookup found no lyrics (not found or instrumental) before <paramref name="lookUpAgainBefore"/>; within both the
    /// ones found first first.
    /// </summary>
    /// <param name="limit">Maximum number of tracks.</param>
    /// <param name="excluded">Tracks to leave out, e.g. those a running job already handled.</param>
    /// <param name="lookUpAgainBefore">Tracks whose lookup found no lyrics before this time are looked up again;
    /// <c>null</c> looks up no track again.</param>
    /// <param name="cancellationToken">Aborts the query.</param>
    Task<IReadOnlyList<LyricsTrackRow>> ListTracksToLookUpAsync(
        int limit,
        IReadOnlyCollection<Guid> excluded,
        DateTimeOffset? lookUpAgainBefore,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the result of a lookup and replaces an earlier one of the track. Returns <c>false</c> if the track no
    /// longer exists.
    /// </summary>
    Task<bool> SaveDownloadAsync(LyricsDownload download, CancellationToken cancellationToken = default);
}
