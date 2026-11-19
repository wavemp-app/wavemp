using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Reads what the library check needs and stores and lists the problems it found. Nothing is tracked.
/// </summary>
public interface ILibraryIssueRepository
{
    /// <summary>
    /// All tracks with what the check needs, ordered by media source, folder and file name.
    /// </summary>
    Task<IReadOnlyList<LibraryCheckTrackRow>> ListTracksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// All files that the scan could not read completely, ordered by media source and path.
    /// </summary>
    Task<IReadOnlyList<LibraryCheckFileRow>> ListUnreadableFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the stored problems with <paramref name="issues"/> in one transaction and stores the time of the run:
    /// problems that are gone are removed, problems found again keep their id and the time they were found first (their
    /// details are updated), new problems are added with <paramref name="checkedAt"/>. Problems of tracks and files that
    /// were removed meanwhile are skipped.
    /// </summary>
    Task<LibraryIssueChanges> ReplaceAsync(
        IReadOnlyCollection<DetectedLibraryIssue> issues,
        int trackCount,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the problems, sorted, filtered and searched.
    /// </summary>
    Task<Page<LibraryIssueRow>> ListAsync(LibraryIssueListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// The number of problems per kind; kinds without problems are missing.
    /// </summary>
    Task<IReadOnlyDictionary<LibraryIssueType, int>> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The last completed run of the check; <c>null</c> if the check never ran.
    /// </summary>
    Task<LibraryCheck?> FindLastCheckAsync(CancellationToken cancellationToken = default);
}
