namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// Downloads the lyrics of a track from LRCLIB and keeps them: as <c>.lrc</c> file with the base name of the track if
/// its folder is writable, otherwise in the database. Only while the download is switched on in the library settings.
/// Lyrics of another version of the song (other duration) are kept without their times and marked.
/// </summary>
public interface ILyricsDownloader
{
    /// <summary>
    /// Looks up the lyrics of the track and saves them. The result of the lookup is stored with the track, so the
    /// download job does not look it up again.
    /// </summary>
    /// <param name="trackId">Id of the track.</param>
    /// <param name="mode">Manual downloads (an admin asks for a track) also replace lyrics of the tags or the database;
    /// automatic ones (the job) skip tracks that have lyrics.</param>
    /// <param name="cancellationToken">Aborts the download.</param>
    Task<LyricsDownloadResult> DownloadAsync(
        Guid trackId,
        LyricsDownloadMode mode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the lyrics of a track of LRCLIB an admin chose from the search (see
    /// <see cref="Searches.ILyricsSearchService"/>) and saves them; they replace an existing lyrics file. If the
    /// duration of the chosen track does not fit, only its plain lyrics are kept, marked as another version.
    /// </summary>
    /// <param name="trackId">Id of the track.</param>
    /// <param name="matchId">Id of the track of LRCLIB.</param>
    /// <param name="cancellationToken">Aborts the download.</param>
    Task<LyricsDownloadResult> DownloadMatchAsync(
        Guid trackId,
        long matchId,
        CancellationToken cancellationToken = default);
}
