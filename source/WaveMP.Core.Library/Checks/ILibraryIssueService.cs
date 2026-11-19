using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Core.Library.Checks;

/// <summary>
/// The problems the library check found: missing covers and tags, unknown albums, possible duplicates, unreadable
/// files and, as hints, videos without audio and songs without video. The check runs as job after every scan and when
/// an admin starts it.
/// </summary>
public interface ILibraryIssueService
{
    /// <summary>
    /// Lists the problems, sorted by path by default, filtered and searched.
    /// </summary>
    Task<Page<LibraryIssueRow>> ListAsync(
        LibraryIssueListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The number of problems per kind and when the check ran last.
    /// </summary>
    Task<LibraryIssueSummary> GetSummaryAsync(CancellationToken cancellationToken = default);
}
