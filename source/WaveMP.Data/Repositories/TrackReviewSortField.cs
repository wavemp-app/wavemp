namespace WaveMP.Data.Repositories;

/// <summary>
/// Sort keys of the review inbox. Artist and Album keep the tracks of an album in the order of disc and track number.
/// </summary>
public enum TrackReviewSortField
{
    /// <summary>
    /// When the track was found for the first time ("date added").
    /// </summary>
    FirstSeenAt,
    Artist,
    Album,
    Title,
}
