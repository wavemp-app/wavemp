namespace WaveMP.Data.Repositories;

/// <summary>
/// Songs that moved from one artist to another (a stored <see cref="Entities.ArtistMove"/>).
/// </summary>
internal sealed record ArtistSuccessionMove(Guid FromArtistId, Guid ToArtistId, int TrackCount, DateTimeOffset MovedAt);
