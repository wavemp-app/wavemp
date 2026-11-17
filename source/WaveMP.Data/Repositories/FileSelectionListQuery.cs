using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Paging, sorting, filter and search of the overview of the file selection.
/// </summary>
/// <param name="Offset">Number of tracks to skip.</param>
/// <param name="Limit">Maximum number of tracks of the page.</param>
/// <param name="Sort">Sort key; ties are sorted by id.</param>
/// <param name="Direction">Sort direction of the key.</param>
/// <param name="Filter">Which tracks to list.</param>
/// <param name="Search">Only tracks whose title or path contains the text, without regard to case.</param>
public sealed record FileSelectionListQuery(
    int Offset,
    int Limit,
    FileSelectionSortField Sort,
    SortDirection Direction,
    FileSelectionFilter Filter = FileSelectionFilter.All,
    string? Search = null);
