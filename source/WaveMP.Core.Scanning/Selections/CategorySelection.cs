using WaveMP.Data.Projections;

namespace WaveMP.Core.Scanning.Selections;

/// <summary>
/// The file a track uses in one category of the file selection, and the files it could use.
/// </summary>
/// <param name="State">Whether the quality rule chooses, the admin selected a file, or the selected file is gone.
/// </param>
/// <param name="SelectedFileId">Id of the selected file; for a missing selection the id the file had.</param>
/// <param name="SelectedPath">Path of the selected file relative to the media folder, as it was selected.</param>
/// <param name="ActiveFileId">Id of the file the track uses; <c>null</c> if the track has no file of the category.
/// </param>
/// <param name="AutomaticFileId">Id of the file the quality rule chooses.</param>
/// <param name="CandidateFileIds">Ids of the files that can be selected, in the order of the quality rule (the best
/// first).</param>
public sealed record CategorySelection(
    FileSelectionState State,
    Guid? SelectedFileId,
    string? SelectedPath,
    Guid? ActiveFileId,
    Guid? AutomaticFileId,
    IReadOnlyList<Guid> CandidateFileIds);
