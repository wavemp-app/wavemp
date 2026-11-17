namespace WaveMP.Core.Lyrics.Services;

/// <summary>
/// Whether a track has lyrics.
/// </summary>
public enum LyricsStatus
{
    Found,

    /// <summary>
    /// The track has no lyrics in any source.
    /// </summary>
    NoLyrics,

    /// <summary>
    /// The track does not exist.
    /// </summary>
    TrackNotFound,
}
