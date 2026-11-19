namespace WaveMP.Service.Dtos;

/// <summary>
/// The result of applying a match to an album.
/// </summary>
public sealed class OnlineAlbumApplyResultDto
{
    /// <summary>
    /// Id of the album afterwards: a new title or album artist makes the songs another album.
    /// </summary>
    public required Guid AlbumId { get; init; }

    /// <summary>
    /// Number of songs whose tags were written.
    /// </summary>
    public required int TracksWritten { get; init; }

    /// <summary>
    /// Number of songs that kept their tags: without audio file or not writable.
    /// </summary>
    public required int TracksSkipped { get; init; }

    /// <summary>
    /// Where the cover was stored.
    /// </summary>
    public required OnlineCoverOutcomeDto Cover { get; init; }
}
