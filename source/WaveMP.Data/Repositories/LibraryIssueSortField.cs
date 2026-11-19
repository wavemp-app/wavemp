namespace WaveMP.Data.Repositories;

/// <summary>
/// Sort keys of the problems of the library.
/// </summary>
public enum LibraryIssueSortField
{
    /// <summary>
    /// Media source, folder and file name of the track or file.
    /// </summary>
    Path,

    /// <summary>
    /// Artist, then album and title of the track.
    /// </summary>
    Artist,

    /// <summary>
    /// Title of the track.
    /// </summary>
    Title,

    /// <summary>
    /// When the check found the problem first.
    /// </summary>
    DetectedAt,
}
