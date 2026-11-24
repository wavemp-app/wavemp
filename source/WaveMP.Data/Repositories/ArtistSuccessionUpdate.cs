namespace WaveMP.Data.Repositories;

/// <summary>
/// The new first seen date and MusicBrainz artist of an artist that took over the data of artists left empty.
/// </summary>
internal sealed record ArtistSuccessionUpdate(
    Guid ArtistId,
    DateTimeOffset FirstSeenAt,
    Guid? MusicBrainzArtistId = null);
