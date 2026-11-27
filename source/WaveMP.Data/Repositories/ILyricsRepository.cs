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
    /// Tracks without lyrics file to look up online: first those that were never looked up, then those looked up
    /// before whose result may be out of date; within both the ones found first first. A result is out of date if the
    /// track already had lyrics (<see cref="LyricsDownloadStatus.NotNeeded"/>) and a scan read one of its primary
    /// audio or video files after the check (the tags may have lost their lyrics, or the file was replaced), or if
    /// the lookup found no lyrics or the track already had lyrics before <paramref name="lookUpAgainBefore"/>, or if
    /// the lookup found nothing with rules older than <paramref name="matcherVersion"/>.
    /// </summary>
    /// <param name="limit">Maximum number of tracks.</param>
    /// <param name="excluded">Tracks to leave out, e.g. those a running job already handled.</param>
    /// <param name="lookUpAgainBefore">Tracks whose lookup found no lyrics (not found or instrumental) or that already
    /// had lyrics before this time are looked up again; <c>null</c> looks up only the tracks with lyrics again whose
    /// files a scan read after the check.</param>
    /// <param name="matcherVersion">The current version of the rules that choose the lyrics; tracks whose lookup
    /// found nothing with an older version are looked up again at once.</param>
    /// <param name="cancellationToken">Aborts the query.</param>
    Task<IReadOnlyList<LyricsTrackRow>> ListTracksToLookUpAsync(
        int limit,
        IReadOnlyCollection<Guid> excluded,
        DateTimeOffset? lookUpAgainBefore,
        int matcherVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the result of a lookup and replaces an earlier one of the track. Returns <c>false</c> if the track no
    /// longer exists.
    /// </summary>
    Task<bool> SaveDownloadAsync(LyricsDownload download, CancellationToken cancellationToken = default);

    /// <summary>
    /// The number of stored lookup results per status, over all tracks. Statuses without a result are missing.
    /// </summary>
    Task<IReadOnlyDictionary<LyricsDownloadStatus, int>> CountDownloadsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the stored lookup results with one of the given statuses, of all tracks or of one track, so the
    /// download job looks these tracks up again like tracks that were never looked up. Returns the number of removed
    /// results.
    /// </summary>
    /// <param name="statuses">Statuses of the results to remove.</param>
    /// <param name="trackId">Only the result of this track; <c>null</c> for all tracks.</param>
    /// <param name="cancellationToken">Aborts the removal.</param>
    Task<int> DeleteDownloadsAsync(
        IReadOnlyCollection<LyricsDownloadStatus> statuses,
        Guid? trackId = null,
        CancellationToken cancellationToken = default);
}
