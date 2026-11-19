namespace WaveMP.Data.Entities;

/// <summary>
/// Where a cover comes from. Stored as its name.
/// </summary>
public enum CoverSource
{
    /// <summary>
    /// A picture file next to the media files.
    /// </summary>
    File,

    /// <summary>
    /// A picture embedded in an audio or video file.
    /// </summary>
    Embedded,

    /// <summary>
    /// A picture found online and kept in the data folder, because the media folder could not be written (see
    /// <see cref="OnlineCover"/>). Only covers that are read from the database carry it; the scan never creates one.
    /// </summary>
    Online,
}
