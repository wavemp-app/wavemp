using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// An artist of a song or album in the order of its artists.
/// </summary>
/// <param name="Id">Id of the artist.</param>
/// <param name="Name">Name of the artist.</param>
/// <param name="Role">Main or featured artist; album artists are always main artists.</param>
public sealed record ArtistCreditRow(Guid Id, string Name, ArtistRole Role = ArtistRole.Main);
