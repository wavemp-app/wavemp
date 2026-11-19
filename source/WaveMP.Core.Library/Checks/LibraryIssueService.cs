using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Checks;

internal sealed class LibraryIssueService(ILibraryIssueRepository repository) : ILibraryIssueService
{
    public Task<Page<LibraryIssueRow>> ListAsync(
        LibraryIssueListRequest request,
        CancellationToken cancellationToken = default)
    {
        LibraryIssueSortField sort = request.Sort ?? LibraryIssueSortField.Path;
        SortDirection direction = request.Direction
            ?? (sort == LibraryIssueSortField.DetectedAt ? SortDirection.Desc : SortDirection.Asc);
        string? search = string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim();
        IReadOnlyCollection<LibraryIssueType>? types = TypesOf(request.Types, request.Severity);
        if (types is { Count: 0 })
        {
            // The kinds asked for and the severity have nothing in common.
            return Task.FromResult(new Page<LibraryIssueRow>([], request.Offset, request.Limit, 0));
        }

        return repository.ListAsync(
            new LibraryIssueListQuery(request.Offset, request.Limit, sort, direction, types, search),
            cancellationToken);
    }

    public async Task<LibraryIssueSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<LibraryIssueType, int> counts = await repository.CountAsync(cancellationToken);
        LibraryCheck? check = await repository.FindLastCheckAsync(cancellationToken);
        return new LibraryIssueSummary(
            [
                .. LibraryIssueTypes.All.Select(type => new LibraryIssueCount(
                    type,
                    LibraryIssueTypes.SeverityOf(type),
                    counts.GetValueOrDefault(type))),
            ],
            check?.CheckedAt,
            check?.TrackCount);
    }

    /// <summary>
    /// The kinds to list in the order of <see cref="LibraryIssueTypes.All"/>: those asked for that have the severity;
    /// <c>null</c> for all.
    /// </summary>
    internal static IReadOnlyCollection<LibraryIssueType>? TypesOf(
        IReadOnlyCollection<LibraryIssueType>? types,
        LibraryIssueSeverity? severity)
    {
        HashSet<LibraryIssueType>? wanted = types is { Count: > 0 } ? [.. types] : null;
        if (severity is { } level)
        {
            IReadOnlyList<LibraryIssueType> ofSeverity = LibraryIssueTypes.OfSeverity(level);
            wanted = wanted is null ? [.. ofSeverity] : [.. ofSeverity.Where(wanted.Contains)];
        }

        return wanted is null ? null : [.. LibraryIssueTypes.All.Where(wanted.Contains)];
    }
}
