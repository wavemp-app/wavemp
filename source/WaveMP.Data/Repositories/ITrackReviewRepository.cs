using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Reads and changes the review status of tracks: whether an admin checked their metadata. Nothing is tracked.
/// </summary>
public interface ITrackReviewRepository
{
    /// <summary>
    /// Lists the tracks of a review status with the fields their tags lack.
    /// </summary>
    Task<Page<TrackReviewRow>> ListAsync(TrackReviewListQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the tracks per review status.
    /// </summary>
    Task<TrackReviewCounts> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The review status of a track; <c>null</c> if the track does not exist.
    /// </summary>
    Task<TrackReviewStateRow?> FindAsync(Guid trackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the unreviewed tracks among <paramref name="trackIds"/> as reviewed by the profile at the time; tracks
    /// that are reviewed already keep their time and profile, unknown ids are skipped. A profile that no longer exists
    /// is not stored. Returns the number of tracks that changed.
    /// </summary>
    Task<int> MarkReviewedAsync(
        IReadOnlyCollection<Guid> trackIds,
        Guid userId,
        DateTimeOffset reviewedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the reviewed tracks among <paramref name="trackIds"/> as unreviewed again; unknown ids are skipped.
    /// Returns the number of tracks that changed.
    /// </summary>
    Task<int> MarkUnreviewedAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken = default);
}
