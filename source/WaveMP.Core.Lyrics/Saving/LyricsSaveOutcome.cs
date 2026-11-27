namespace WaveMP.Core.Lyrics.Saving;

/// <summary>
/// What saving lyrics did.
/// </summary>
public enum LyricsSaveOutcome
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
    /// The track has a lyrics file, which is only replaced by lyrics an admin chose or uploaded.
    /// </summary>
    FileExists,

    /// <summary>
    /// The lyrics file of the track cannot be replaced (read-only folder or file).
    /// </summary>
    FileNotWritable,

    /// <summary>
    /// The track does not exist (any more).
    /// </summary>
    TrackNotFound,
}
