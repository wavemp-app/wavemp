namespace WaveMP.Data.Repositories;

/// <summary>
/// The new first seen date of an artist that took over the data of artists left empty.
/// </summary>
internal sealed record ArtistSuccessionUpdate(Guid ArtistId, DateTimeOffset FirstSeenAt);
