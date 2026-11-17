namespace WaveMP.Service.Dtos;

/// <summary>
/// The review status of a track.
/// </summary>
public sealed class TrackReviewStateDto
{
    public required Guid TrackId { get; init; }

    public required TrackReviewStatusDto Status { get; init; }

    /// <summary>
    /// When the track was marked as reviewed; null while unreviewed.
    /// </summary>
    public DateTimeOffset? ReviewedAt { get; init; }

    /// <summary>
    /// The profile that marked the track as reviewed; null while unreviewed or after the profile was deleted.
    /// </summary>
    public ProfileReferenceDto? ReviewedBy { get; init; }
}
