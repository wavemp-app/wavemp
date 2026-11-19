namespace WaveMP.Service.Dtos;

/// <summary>
/// The tags of an album.
/// </summary>
public sealed class OnlineAlbumValuesDto
{
    public string? Album { get; init; }

    public string? AlbumArtist { get; init; }

    public int? Year { get; init; }

    public string? Genre { get; init; }
}
