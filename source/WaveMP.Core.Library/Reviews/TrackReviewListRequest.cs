using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Reviews;

/// <summary>
/// A request for a page of the review inbox as the client sends it.
/// </summary>
/// <param name="Offset">Number of tracks to skip.</param>
/// <param name="Limit">Maximum number of tracks of the page.</param>
/// <param name="Sort">Sort key; the time found first by default.</param>
/// <param name="Direction">Sort direction; descending (the newest first) for the time found first, otherwise
/// ascending.</param>
/// <param name="Status">Review status of the tracks; unreviewed by default.</param>
/// <param name="MissingTags">Only tracks whose tags lack a checked field (<c>true</c>) or have all of them
/// (<c>false</c>).</param>
/// <param name="Search">Search text as typed; empty texts search nothing.</param>
public sealed record TrackReviewListRequest(
    int Offset,
    int Limit,
    TrackReviewSortField? Sort = null,
    SortDirection? Direction = null,
    TrackReviewStatus? Status = null,
    bool? MissingTags = null,
    string? Search = null);
