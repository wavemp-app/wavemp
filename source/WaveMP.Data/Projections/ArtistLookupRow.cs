namespace WaveMP.Data.Projections;

/// <summary>
/// What the online lookup of artist pictures needs of an artist.
/// </summary>
/// <param name="Id">Id of the artist.</param>
/// <param name="Name">Name of the artist.</param>
/// <param name="MusicBrainzArtistId">The MusicBrainz artist an admin chose earlier.</param>
public sealed record ArtistLookupRow(Guid Id, string Name, Guid? MusicBrainzArtistId);
