namespace WaveMP.Data.Entities;

/// <summary>
/// The kind of file the admin selects for a track. Stored as its name.
/// </summary>
public enum FileSelectionCategory
{
    /// <summary>
    /// The audio file the track plays.
    /// </summary>
    Audio,

    /// <summary>
    /// The video file the track plays.
    /// </summary>
    Video,

    /// <summary>
    /// The cover of the track: a picture file with its base name or a picture embedded in one of its audio or video
    /// files (stored as the id of that file).
    /// </summary>
    Cover,
}
