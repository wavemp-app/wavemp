using WaveMP.Data.Entities;

namespace WaveMP.Data.Projections;

/// <summary>
/// An alias of an artist that the search finds it by.
/// </summary>
/// <param name="Id">Id of the alias.</param>
/// <param name="ArtistId">Id of the artist.</param>
/// <param name="Name">The alias.</param>
/// <param name="Source">Where the alias came from.</param>
public sealed record ArtistAliasRow(Guid Id, Guid ArtistId, string Name, ArtistAliasSource Source);
