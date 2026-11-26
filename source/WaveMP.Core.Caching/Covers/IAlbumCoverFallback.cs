namespace WaveMP.Core.Caching.Covers;

/// <summary>
/// The picture an album shows when none of its own covers can be read and its rule allows it: the picture of its
/// album artist. Implemented by the area of the artist pictures, which delivers them from its own store.
/// </summary>
public interface IAlbumCoverFallback
{
    /// <summary>
    /// The real picture of the artist (in its folder, uploaded or taken over from the online lookup), scaled to the
    /// size; <c>null</c> if the artist has none. The collage of album covers of an artist is never delivered.
    /// </summary>
    /// <param name="artistId">Id of the album artist.</param>
    /// <param name="size">Requested width and height in pixels; <c>null</c> for the size the artist pictures use
    /// without one.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    Task<CoverDelivery?> DeliverArtistPictureAsync(
        Guid artistId,
        int? size,
        CancellationToken cancellationToken = default);
}
