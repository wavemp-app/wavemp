using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Paging, sorting, filter and search of the review inbox.
/// </summary>
/// <param name="Offset">Number of tracks to skip.</param>
/// <param name="Limit">Maximum number of tracks of the page.</param>
/// <param name="Sort">Sort key; ties are sorted by id.</param>
/// <param name="Direction">Sort direction of the key.</param>
/// <param name="Status">Only tracks with this review status.</param>
/// <param name="MissingTags">Only tracks whose tags lack a checked field (<c>true</c>) or have all of them
/// (<c>false</c>); <c>null</c> for both.</param>
/// <param name="Search">Only tracks whose title, artist, album, folder or file name contains the text, without regard
/// to case.</param>
public sealed record TrackReviewListQuery(
    int Offset,
    int Limit,
    TrackReviewSortField Sort,
    SortDirection Direction,
    TrackReviewStatus Status = TrackReviewStatus.Unreviewed,
    bool? MissingTags = null,
    string? Search = null);
