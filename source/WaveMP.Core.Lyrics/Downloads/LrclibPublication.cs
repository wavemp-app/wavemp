namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// Lyrics an admin publishes to LRCLIB, with the names and the duration of the track they belong to.
/// </summary>
/// <param name="TrackName">Title of the track.</param>
/// <param name="ArtistName">Artist of the track.</param>
/// <param name="AlbumName">Album of the track.</param>
/// <param name="Duration">Duration of the track.</param>
/// <param name="PlainLyrics">The lines without times.</param>
/// <param name="SyncedLyrics">The lines in the LRC format with one timestamp per line, or an empty text for lyrics
/// without times.</param>
public sealed record LrclibPublication(
    string TrackName,
    string ArtistName,
    string AlbumName,
    TimeSpan Duration,
    string PlainLyrics,
    string SyncedLyrics);
