namespace WaveMP.Data.Entities;

/// <summary>
/// Result of the song analysis of a media file. Stored as its name.
/// </summary>
public enum SongAnalysisStatus
{
    /// <summary>
    /// The sound was analyzed.
    /// </summary>
    Analyzed,

    /// <summary>
    /// The file has no sound (e.g. a video without audio track); the whole file counts as the song.
    /// </summary>
    NoSound,

    /// <summary>
    /// The sound could not be read; the whole file counts as the song. A changed file is analyzed again.
    /// </summary>
    Failed,
}
