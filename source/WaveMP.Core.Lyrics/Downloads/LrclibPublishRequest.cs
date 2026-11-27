namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// The body of <c>POST api/publish</c> of LRCLIB.
/// </summary>
/// <param name="TrackName"></param>
/// <param name="ArtistName"></param>
/// <param name="AlbumName"></param>
/// <param name="Duration">Duration in seconds.</param>
/// <param name="PlainLyrics"></param>
/// <param name="SyncedLyrics"></param>
internal sealed record LrclibPublishRequest(
    string TrackName,
    string ArtistName,
    string AlbumName,
    double Duration,
    string PlainLyrics,
    string SyncedLyrics);
