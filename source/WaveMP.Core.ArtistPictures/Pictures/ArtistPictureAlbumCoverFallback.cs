using WaveMP.Core.Caching.Covers;
using WaveMP.Core.Library.Covers;

namespace WaveMP.Core.ArtistPictures.Pictures;

/// <summary>
/// Delivers the shown picture of an artist as the cover of an album without a cover of its own: only a real picture
/// (in the folder of the artist, uploaded or taken over from the online lookup), never the collage of its album
/// covers, which clients build themselves.
/// </summary>
internal sealed class ArtistPictureAlbumCoverFallback(IArtistPictureService pictures) : IAlbumCoverFallback
{
    public async Task<CoverDelivery?> DeliverArtistPictureAsync(
        Guid artistId,
        int? size,
        CancellationToken cancellationToken = default)
    {
        CoverDeliveryResult result = await pictures.GetShownAsync(artistId, size, cancellationToken);
        return result.Status == CoverStatus.Found ? result.Delivery : null;
    }
}
