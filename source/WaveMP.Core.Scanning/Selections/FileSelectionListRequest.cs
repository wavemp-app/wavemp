using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scanning.Selections;

/// <summary>
/// A request for a page of the overview of the file selection as the client sends it.
/// </summary>
/// <param name="Offset">Number of tracks to skip.</param>
/// <param name="Limit">Maximum number of tracks of the page.</param>
/// <param name="Sort">Sort key; the path by default.</param>
/// <param name="Direction">Sort direction; ascending by default, descending for the number of files.</param>
/// <param name="Filter">Which tracks to list; all by default.</param>
/// <param name="Search">Search text as typed; empty texts search nothing.</param>
public sealed record FileSelectionListRequest(
    int Offset,
    int Limit,
    FileSelectionSortField? Sort = null,
    SortDirection? Direction = null,
    FileSelectionFilter? Filter = null,
    string? Search = null);
