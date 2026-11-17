using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Core.Scanning.Selections;

/// <summary>
/// Lets the admin decide per track which audio, video and cover file it uses instead of the automatic choice by
/// quality. A change is applied to the library at once (primary files, metadata from the selected audio file,
/// covers) and stays across scans; a selected file that is deleted leaves the track with the automatic choice and the
/// selection marked as missing.
/// </summary>
public interface IFileSelectionService
{
    /// <summary>
    /// Lists the tracks with their number of files per category and the state of their selections.
    /// </summary>
    Task<Page<TrackFileSummaryRow>> ListAsync(
        FileSelectionListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The file selection of a track; <c>null</c> if the track does not exist.
    /// </summary>
    Task<FileSelectionDetails?> GetAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the selection of the track: a file per category, <c>null</c> for the automatic choice. A category
    /// keeps a missing selection if its id is sent again. Waits while a scan writes the library.
    /// </summary>
    Task<FileSelectionResult> SetAsync(
        Guid trackId,
        FileChoices choices,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all selections of the track, so the quality rule chooses again. Returns <c>false</c> if the track does
    /// not exist.
    /// </summary>
    Task<bool> ResetAsync(Guid trackId, CancellationToken cancellationToken = default);
}
