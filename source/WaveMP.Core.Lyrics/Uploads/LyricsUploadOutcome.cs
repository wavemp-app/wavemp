namespace WaveMP.Core.Lyrics.Uploads;

/// <summary>
/// What saving uploaded lyrics did.
/// </summary>
public enum LyricsUploadOutcome
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
    /// The text has no line with text.
    /// </summary>
    Empty,

    /// <summary>
    /// The lyrics file of the track cannot be replaced (read-only folder or file).
    /// </summary>
    FileNotWritable,

    /// <summary>
    /// The track does not exist.
    /// </summary>
    TrackNotFound,
}
