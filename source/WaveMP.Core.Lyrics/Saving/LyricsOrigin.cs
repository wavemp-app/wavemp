namespace WaveMP.Core.Lyrics.Saving;

/// <summary>
/// Where saved lyrics come from.
/// </summary>
public enum LyricsOrigin
{
    /// <summary>
    /// Downloaded from LRCLIB, found by the lookup or chosen by an admin.
    /// </summary>
    Download,

    /// <summary>
    /// Uploaded by an admin.
    /// </summary>
    Upload,
}
