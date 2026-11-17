namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// Who asks for a download.
/// </summary>
public enum LyricsDownloadMode
{
    /// <summary>
    /// An admin, for one track; lyrics of the tags or the database are replaced.
    /// </summary>
    Manual,

    /// <summary>
    /// The download job; tracks that have lyrics are skipped.
    /// </summary>
    Automatic,
}
