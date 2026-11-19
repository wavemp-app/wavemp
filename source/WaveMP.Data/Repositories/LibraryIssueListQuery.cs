using WaveMP.Data.Entities;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Paging, sorting, filter and search of the problems of the library.
/// </summary>
/// <param name="Offset">Number of problems to skip.</param>
/// <param name="Limit">Maximum number of problems of the page.</param>
/// <param name="Sort">Sort key; ties are sorted by path, type and id.</param>
/// <param name="Direction">Sort direction of the key.</param>
/// <param name="Types">Only problems of these kinds; <c>null</c> for all.</param>
/// <param name="Search">Only problems whose track title, artist, album, folder, file name or file path contains the
/// text, without regard to case.</param>
public sealed record LibraryIssueListQuery(
    int Offset,
    int Limit,
    LibraryIssueSortField Sort,
    SortDirection Direction,
    IReadOnlyCollection<LibraryIssueType>? Types = null,
    string? Search = null);
