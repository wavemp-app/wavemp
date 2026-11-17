namespace WaveMP.Data.Projections;

/// <summary>
/// The review status of a track.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="ReviewedAt">When an admin marked the track as reviewed; <c>null</c> while unreviewed.</param>
/// <param name="ReviewedByUserId">Id of the profile that marked it; <c>null</c> while unreviewed or after the profile
/// was deleted.</param>
/// <param name="ReviewedByName">Name of that profile.</param>
public sealed record TrackReviewStateRow(
    Guid TrackId,
    DateTimeOffset? ReviewedAt,
    Guid? ReviewedByUserId,
    string? ReviewedByName)
{
    public bool IsReviewed => ReviewedAt is not null;
}
