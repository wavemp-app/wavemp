namespace WaveMP.Data.Repositories;

/// <summary>
/// Which tracks the overview of the file selection lists.
/// </summary>
public enum FileSelectionFilter
{
    All,

    /// <summary>
    /// Tracks with more than one audio file.
    /// </summary>
    SeveralAudio,

    /// <summary>
    /// Tracks with more than one video file.
    /// </summary>
    SeveralVideo,

    /// <summary>
    /// Tracks with more than one cover of their own.
    /// </summary>
    SeveralCovers,

    /// <summary>
    /// Tracks with a choice: more than one audio file, video file or cover of their own.
    /// </summary>
    Choices,

    /// <summary>
    /// Tracks with at least one selection by the admin, also a missing one.
    /// </summary>
    Selected,

    /// <summary>
    /// Tracks with a selection whose file is gone.
    /// </summary>
    Missing,
}
