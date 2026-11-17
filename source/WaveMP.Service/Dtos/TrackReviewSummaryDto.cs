namespace WaveMP.Service.Dtos;

/// <summary>
/// Number of tracks per review status, for example for the badge of the review inbox.
/// </summary>
public sealed class TrackReviewSummaryDto
{
    public required int UnreviewedCount { get; init; }

    public required int ReviewedCount { get; init; }
}
