namespace WaveMP.Data.Repositories;

/// <summary>
/// Number of tracks per review status.
/// </summary>
public sealed record TrackReviewCounts(int Unreviewed, int Reviewed);
