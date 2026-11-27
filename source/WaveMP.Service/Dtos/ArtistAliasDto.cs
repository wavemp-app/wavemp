namespace WaveMP.Service.Dtos;

/// <summary>
/// A further name of an artist that the search finds it by, e.g. "Alecia Moore" for "P!nk". Search results always show
/// the name of the artist.
/// </summary>
public sealed class ArtistAliasDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required ArtistAliasSourceDto Source { get; init; }
}
