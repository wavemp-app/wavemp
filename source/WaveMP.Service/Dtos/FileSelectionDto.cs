namespace WaveMP.Service.Dtos;

/// <summary>
/// The file a track uses in one category of the file selection, and the files it could use.
/// </summary>
public sealed class FileSelectionDto
{
    public required FileSelectionStateDto State { get; init; }

    /// <summary>
    /// Id of the file the admin selected; for a missing selection the id the file had. Null without selection.
    /// </summary>
    public Guid? SelectedFileId { get; init; }

    /// <summary>
    /// Path of the selected file relative to the media folder, as it was selected. Null without selection.
    /// </summary>
    public string? SelectedPath { get; init; }

    /// <summary>
    /// Id of the file the track uses (plays or shows); null if the track has no file of the category.
    /// </summary>
    public Guid? ActiveFileId { get; init; }

    /// <summary>
    /// Id of the file the quality rule chooses without selection.
    /// </summary>
    public Guid? AutomaticFileId { get; init; }

    /// <summary>
    /// Ids of the files that can be selected (see ListTrackFiles), in the order of the quality rule, the best first.
    /// Covers are the readable pictures with the base name of the track and the audio and video files with an
    /// embedded picture.
    /// </summary>
    public required IReadOnlyList<Guid> CandidateFileIds { get; init; }
}
