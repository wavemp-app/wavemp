namespace WaveMP.Core.Lyrics.Searches;

/// <summary>
/// The search of admins for the lyrics of a track in LRCLIB, with a title and an artist they may change, so they can
/// choose the lyrics themselves (see <see cref="Downloads.ILyricsDownloader.DownloadMatchAsync"/>). Only while the
/// download is switched on in the library settings.
/// </summary>
public interface ILyricsSearchService
{
    /// <summary>
    /// Searches LRCLIB by title and artist, and by the free-text search if that finds nothing.
    /// </summary>
    /// <param name="trackId">Id of the track.</param>
    /// <param name="title">The title to search for; <c>null</c> or empty for the title the lookup would use, without
    /// additions such as <c>"(Remastered)"</c>.</param>
    /// <param name="artist">The artist to search for; <c>null</c> for the main artist the lookup would use, empty to
    /// search without artist.</param>
    /// <param name="cancellationToken">Aborts the search.</param>
    Task<LyricsSearchResult> SearchAsync(
        Guid trackId,
        string? title,
        string? artist,
        CancellationToken cancellationToken = default);
}
