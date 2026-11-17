using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Sets the review status of a track.
/// </summary>
public sealed class UpdateTrackReviewDto
{
    /// <summary>
    /// Reviewed marks the track as reviewed by the current profile now; Unreviewed puts it back into the review inbox.
    /// </summary>
    [Required]
    public required TrackReviewStatusDto? Status { get; init; }
}
