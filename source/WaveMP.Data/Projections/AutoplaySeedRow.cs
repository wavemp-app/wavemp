namespace WaveMP.Data.Projections;

/// <summary>
/// A track of the list autoplay continues: what the choice of the following songs is based on.
/// </summary>
/// <param name="TrackId">Id of the track.</param>
/// <param name="ArtistIds">Ids of all artists of the track in their order; empty if unknown.</param>
/// <param name="Genre">Genre of the track; <c>null</c> if unknown.</param>
/// <param name="Year">Year of the track; <c>null</c> if unknown.</param>
public sealed record AutoplaySeedRow(Guid TrackId, IReadOnlyList<Guid> ArtistIds, string? Genre, int? Year);
