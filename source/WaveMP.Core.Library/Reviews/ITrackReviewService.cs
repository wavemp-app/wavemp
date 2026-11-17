using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Library.Reviews;

/// <summary>
/// The review inbox of the admins: which tracks nobody has checked yet. Every track starts unreviewed, also the
/// tracks that existed before the review status; marking it reviewed stores the time and the profile. Scans and the
/// metadata editor never change the status.
/// </summary>
public interface ITrackReviewService
{
    /// <summary>
    /// Lists the tracks of a review status, unreviewed by default, with the fields their tags lack.
    /// </summary>
    Task<Page<TrackReviewRow>> ListAsync(TrackReviewListRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the tracks per review status.
    /// </summary>
    Task<TrackReviewCounts> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The review status of a track; <c>null</c> if the track does not exist.
    /// </summary>
    Task<TrackReviewStateRow?> GetAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the review status of a track and returns it; <c>null</c> if the track does not exist. A track that has
    /// the status already keeps it unchanged, a reviewed one with its time and profile.
    /// </summary>
    Task<TrackReviewStateRow?> SetAsync(
        Guid trackId,
        TrackReviewStatus status,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the review status of several tracks at once (at most <see cref="TrackReviews.MaxBatchSize"/>); unknown
    /// tracks and tracks that have the status already are skipped. Returns the number of tracks that changed.
    /// </summary>
    Task<int> SetManyAsync(
        IReadOnlyCollection<Guid> trackIds,
        TrackReviewStatus status,
        Guid userId,
        CancellationToken cancellationToken = default);
}
