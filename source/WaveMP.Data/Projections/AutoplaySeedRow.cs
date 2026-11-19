namespace WaveMP.Data.Projections;

/// <summary>
/// A track of the list autoplay continues: what the choice of the following songs is based on.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="ArtistId">Id of the artist of the track; <c>null</c> if unknown.</param>
/// <param name="Genre">Genre of the track; <c>null</c> if unknown.</param>
/// <param name="Year">Year of the track; <c>null</c> if unknown.</param>
public sealed record AutoplaySeedRow(Guid TrackId, Guid? ArtistId, string? Genre, int? Year);
