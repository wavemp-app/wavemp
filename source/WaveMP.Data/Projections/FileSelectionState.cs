namespace WaveMP.Data.Projections;

/// <summary>
/// Which file a track uses in a category of the file selection.
/// </summary>
public enum FileSelectionState
{
    /// <summary>
    /// No selection: the file of the highest quality.
    /// </summary>
    Automatic,

    /// <summary>
    /// The file the admin selected.
    /// </summary>
    Selected,

    /// <summary>
    /// The admin selected a file that is gone; the track falls back to the automatic choice.
    /// </summary>
    Missing,
}
