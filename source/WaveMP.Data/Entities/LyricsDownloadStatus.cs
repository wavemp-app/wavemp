namespace WaveMP.Data.Entities;

/// <summary>
/// The result of looking up the lyrics of a track online. Stored as its name.
/// </summary>
public enum LyricsDownloadStatus
{
    /// <summary>
    /// Lyrics were found and saved as <c>.lrc</c> file with the base name of the track.
    /// </summary>
    SavedToFile,

    /// <summary>
    /// Lyrics were found, but the folder of the track is not writable; they are stored in the database.
    /// </summary>
    SavedToDatabase,

    /// <summary>
    /// The service has no lyrics for the track.
    /// </summary>
    NotFound,

    /// <summary>
    /// The service lists the track as instrumental, without lyrics.
    /// </summary>
    Instrumental,

    /// <summary>
    /// The track already had lyrics (a lyrics file or lyrics in its tags), so nothing was downloaded. The download job
    /// checks the track again once a scan read one of its primary audio or video files after the check, and after the
    /// wait for tracks without lyrics, because the tags may have lost their lyrics in the meantime.
    /// </summary>
    NotNeeded,
}
