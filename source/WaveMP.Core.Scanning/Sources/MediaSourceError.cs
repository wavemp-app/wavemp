namespace WaveMP.Core.Scanning.Sources;

/// <summary>
/// Why a media source could not be created, changed or removed.
/// </summary>
public enum MediaSourceError
{
    /// <summary>
    /// The source does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    /// The name is empty after trimming.
    /// </summary>
    InvalidName,

    /// <summary>
    /// Another source has the same name (without regard to case).
    /// </summary>
    NameTaken,

    /// <summary>
    /// The path is not an absolute path.
    /// </summary>
    InvalidPath,

    /// <summary>
    /// The folder does not exist in the container.
    /// </summary>
    PathNotFound,

    /// <summary>
    /// The folder is the folder of another source, lies in it or contains it.
    /// </summary>
    PathOverlaps,

    /// <summary>
    /// A scan is queued or running; the folder of a source cannot change and a source cannot be removed meanwhile.
    /// </summary>
    ScanRunning,
}
