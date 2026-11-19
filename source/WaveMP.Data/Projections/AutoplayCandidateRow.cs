namespace WaveMP.Data.Projections;

/// <summary>
/// A track autoplay may choose, with what the choice weighs for the user.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="ArtistId">Id of the artist of the track; <c>null</c> if unknown.</param>
/// <param name="Genre">Genre of the track; <c>null</c> if unknown.</param>
/// <param name="Year">Year of the track; <c>null</c> if unknown.</param>
/// <param name="HasAudio">Whether the track has an audio file; <c>false</c> for songs with a music video only.</param>
/// <param name="IsFavorite">Whether the user marked the track as favorite.</param>
/// <param name="Rating">Rating of the user, 2 to 5 stars; 0 if the user did not rate the track. Tracks the user rated
/// with 1 star are never candidates.</param>
public sealed record AutoplayCandidateRow(
    Guid TrackId,
    Guid? ArtistId,
    string? Genre,
    int? Year,
    bool HasAudio,
    bool IsFavorite,
    int Rating);
