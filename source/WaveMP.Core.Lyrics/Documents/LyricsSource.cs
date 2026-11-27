namespace WaveMP.Core.Lyrics.Documents;

/// <summary>
/// Where lyrics come from, in the order of their priority.
/// </summary>
public enum LyricsSource
{
    /// <summary>
    /// The lyrics file with the base name of the track (<c>Song.lrc</c>), also one saved by the download.
    /// </summary>
    File,

    /// <summary>
    /// Downloaded lyrics stored in the database, because the folder of the track is not writable.
    /// </summary>
    Download,

    /// <summary>
    /// Lyrics an admin uploaded, stored in the database, because the folder of the track is not writable.
    /// </summary>
    Upload,

    /// <summary>
    /// The tags of the audio file (or of the video file of tracks without audio): synchronized lyrics first, then
    /// unsynchronized ones.
    /// </summary>
    Tags,
}
