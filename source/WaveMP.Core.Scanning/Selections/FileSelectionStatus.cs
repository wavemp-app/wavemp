namespace WaveMP.Core.Scanning.Selections;

/// <summary>
/// Outcome of a change of the file selection.
/// </summary>
public enum FileSelectionStatus
{
    Done,

    TrackNotFound,

    /// <summary>
    /// A selected file is not one of the files of its category that the track can use.
    /// </summary>
    Invalid,
}
