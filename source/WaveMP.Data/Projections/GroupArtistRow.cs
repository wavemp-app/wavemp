namespace WaveMP.Data.Projections;

/// <summary>
/// What a group of a list shows of its artist.
/// </summary>
/// <param name="Id">Id of the artist.</param>
/// <param name="Name">Name of the artist.</param>
public sealed record GroupArtistRow(Guid Id, string Name);
