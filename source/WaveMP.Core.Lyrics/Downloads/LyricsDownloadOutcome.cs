namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// What a download did.
/// </summary>
public enum LyricsDownloadOutcome
{
    /// <summary>
    /// The lyrics were saved as <c>.lrc</c> file next to the track.
    /// </summary>
    SavedToFile,

    /// <summary>
    /// The folder of the track is not writable; the lyrics were saved in the database.
    /// </summary>
    SavedToDatabase,

    /// <summary>
    /// LRCLIB has no lyrics for the track (or no track with the chosen id).
    /// </summary>
    NotFound,

    /// <summary>
    /// LRCLIB lists the track as instrumental.
    /// </summary>
    Instrumental,

    /// <summary>
    /// The track already has lyrics (automatic downloads only); nothing was downloaded.
    /// </summary>
    NotNeeded,

    /// <summary>
    /// The track already has a lyrics file, which is never replaced.
    /// </summary>
    LyricsFileExists,

    /// <summary>
    /// The lyrics file of the track cannot be replaced by the chosen lyrics (read-only folder or file).
    /// </summary>
    FileNotWritable,

    /// <summary>
    /// The download is switched off in the library settings.
    /// </summary>
    Disabled,

    /// <summary>
    /// The track does not exist.
    /// </summary>
    TrackNotFound,

    /// <summary>
    /// LRCLIB refuses requests for a while.
    /// </summary>
    RateLimited,

    /// <summary>
    /// LRCLIB cannot be reached, e.g. without an internet connection.
    /// </summary>
    Unavailable,
}
