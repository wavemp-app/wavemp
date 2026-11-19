using WaveMP.Data.Entities;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Checks;

/// <summary>
/// A request for a page of the problems of the library as the client sends it.
/// </summary>
/// <param name="Offset">Number of problems to skip.</param>
/// <param name="Limit">Maximum number of problems of the page.</param>
/// <param name="Sort">Sort key; the path by default.</param>
/// <param name="Direction">Sort direction; descending (the newest first) for the time found, otherwise ascending.
/// </param>
/// <param name="Types">Only problems of these kinds; all if <c>null</c> or empty.</param>
/// <param name="Severity">Only problems of this severity; together with <paramref name="Types"/> only the kinds of
/// both.</param>
/// <param name="Search">Search text as typed; empty texts search nothing.</param>
public sealed record LibraryIssueListRequest(
    int Offset,
    int Limit,
    LibraryIssueSortField? Sort = null,
    SortDirection? Direction = null,
    IReadOnlyCollection<LibraryIssueType>? Types = null,
    LibraryIssueSeverity? Severity = null,
    string? Search = null);
