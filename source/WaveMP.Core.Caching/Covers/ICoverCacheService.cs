using WaveMP.Data.Entities;

namespace WaveMP.Core.Caching.Covers;

/// <summary>
/// Delivers the covers of tracks and albums following the most specific cover rule (track, album, library settings):
/// scaled from the cache with a size, as stored without one.
/// </summary>
public interface ICoverCacheService
{
    /// <summary>
    /// The cover of a track: by default its own cover, otherwise the cover of its album; never the picture of an
    /// artist. Covers whose file is missing or unreadable are skipped.
    /// </summary>
    /// <param name="trackId">Id of the track.</param>
    /// <param name="size">Requested width and height in pixels, rounded up to a size of <see cref="CoverSizes"/>;
    /// <c>null</c> for the picture as stored.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    Task<CoverDeliveryResult> GetTrackCoverAsync(
        Guid trackId,
        int? size,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The cover of an album: its own cover (online cover, picture in its folder, picture embedded in one of its
    /// tracks); without one, by default the real picture of its album artist (<see cref="IAlbumCoverFallback"/>).
    /// </summary>
    /// <param name="albumId">Id of the album.</param>
    /// <param name="size">Requested width and height in pixels, rounded up to a size of <see cref="CoverSizes"/>;
    /// <c>null</c> for the picture as stored.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    Task<CoverDeliveryResult> GetAlbumCoverAsync(
        Guid albumId,
        int? size,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The cover a track would show with another rule of its own, without changing it.
    /// </summary>
    /// <param name="trackId">Id of the track.</param>
    /// <param name="rule">The rule to try; <c>null</c> inherits the rule of its album.</param>
    /// <param name="size">Requested width and height in pixels; <c>null</c> for the picture as stored.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    Task<CoverDeliveryResult> PreviewTrackCoverAsync(
        Guid trackId,
        CoverPriority? rule,
        int? size,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The cover an album shows; the rule of its songs does not change it.
    /// </summary>
    /// <param name="albumId">Id of the album.</param>
    /// <param name="rule">The rule to try; <c>null</c> inherits the global rule.</param>
    /// <param name="size">Requested width and height in pixels; <c>null</c> for the picture as stored.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    Task<CoverDeliveryResult> PreviewAlbumCoverAsync(
        Guid albumId,
        CoverPriority? rule,
        int? size,
        CancellationToken cancellationToken = default);
}
