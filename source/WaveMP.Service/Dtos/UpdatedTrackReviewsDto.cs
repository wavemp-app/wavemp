namespace WaveMP.Service.Dtos;

/// <summary>
/// Result of setting the review status of several tracks.
/// </summary>
public sealed class UpdatedTrackReviewsDto
{
    /// <summary>
    /// Number of tracks whose status changed; tracks that had the status already and unknown tracks do not count.
    /// </summary>
    public required int ChangedCount { get; init; }
}
