using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Reads the files of tracks and stores the files the admin selects for them. Applying a selection to the library is
/// the task of the scan logic. Nothing is tracked.
/// </summary>
public interface IFileSelectionRepository
{
    /// <summary>
    /// The track with its files, own covers and selections; <c>null</c> if the track does not exist.
    /// </summary>
    Task<TrackFiles?> LoadTrackAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces all selections of the track with <paramref name="selections"/> (an empty list removes them). Returns
    /// <c>false</c> if the track does not exist.
    /// </summary>
    Task<bool> ReplaceAsync(
        Guid trackId,
        IReadOnlyCollection<TrackFileSelection> selections,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the tracks with their number of files per category and the state of their selections.
    /// </summary>
    Task<Page<TrackFileSummaryRow>> ListAsync(
        FileSelectionListQuery query,
        CancellationToken cancellationToken = default);
}
