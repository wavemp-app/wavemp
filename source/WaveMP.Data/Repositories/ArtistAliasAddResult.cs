using WaveMP.Data.Projections;

namespace WaveMP.Data.Repositories;

/// <summary>
/// The outcome of <see cref="IArtistAliasRepository.AddAsync"/> with the alias when it was added.
/// </summary>
public sealed record ArtistAliasAddResult(ArtistAliasAddStatus Status, ArtistAliasRow? Alias = null);
