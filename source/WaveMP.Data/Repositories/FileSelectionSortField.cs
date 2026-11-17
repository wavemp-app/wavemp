namespace WaveMP.Data.Repositories;

/// <summary>
/// Sort key of the overview of the file selection.
/// </summary>
public enum FileSelectionSortField
{
    /// <summary>
    /// Folder, then file name of the track.
    /// </summary>
    Path,

    Title,

    /// <summary>
    /// Total number of audio files, video files and covers of the track, then folder and file name.
    /// </summary>
    FileCount,
}
