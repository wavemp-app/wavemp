namespace WaveMP.Service.Dtos;

/// <summary>
/// A release of MusicBrainz that may be the album, with the tags it suggests.
/// </summary>
public sealed class OnlineAlbumMatchDto
{
    /// <summary>
    /// The release.
    /// </summary>
    public required OnlineReleaseDto Release { get; init; }

    /// <summary>
    /// How well the match agrees with the album, 0 to 100: title, album artist and number of songs compared.
    /// </summary>
    public required int Agreement { get; init; }

    /// <summary>
    /// The tags it suggests.
    /// </summary>
    public required OnlineAlbumValuesDto Values { get; init; }
}
