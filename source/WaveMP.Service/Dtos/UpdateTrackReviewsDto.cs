using System.ComponentModel.DataAnnotations;
using WaveMP.Core.Library.Reviews;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Sets the review status of several tracks at once.
/// </summary>
public sealed class UpdateTrackReviewsDto
{
    /// <summary>
    /// The tracks; unknown ids and duplicates are skipped.
    /// </summary>
    [Required]
    [Length(1, TrackReviews.MaxBatchSize)]
    public required IReadOnlyList<Guid> TrackIds { get; init; }

    /// <summary>
    /// Reviewed marks the tracks as reviewed by the current profile now; Unreviewed puts them back into the review
    /// inbox.
    /// </summary>
    [Required]
    public required TrackReviewStatusDto? Status { get; init; }
}
