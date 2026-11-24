using WaveMP.Core.Caching.Covers;
using WaveMP.Data.Entities;

namespace WaveMP.Core.ArtistPictures.Pictures;

/// <summary>
/// The pictures of the artists. An artist shows the picture file in its folder (found by the scan), otherwise the
/// picture an admin uploaded, otherwise the picture an admin took over from the online lookup; clients show a collage
/// of its album covers when it has none of them (see <see cref="ArtistPictureView"/>).
/// </summary>
public interface IArtistPictureService
{
    /// <summary>
    /// The shown picture of the artist as WebP: the picture file in its folder scaled to the size (rounded up to
    /// 96, 192, 300, 600, 1000 or 2000 pixels, keeping its shape; if it cannot be read, the next source), otherwise the
    /// uploaded, otherwise the online picture as square of 96, 300 or 1000 pixels (the size rounded up). Without a
    /// size, 1000 pixels. <see cref="CoverDeliveryResult.OwnerNotFound"/> for an unknown artist,
    /// <see cref="CoverDeliveryResult.NotFound"/> if it has no picture.
    /// </summary>
    Task<CoverDeliveryResult> GetShownAsync(Guid artistId, int? size, CancellationToken cancellationToken = default);

    /// <summary>
    /// The stored picture of the kind as square WebP of 96, 300 or 1000 pixels (the size rounded up; 1000 without a
    /// size), also while another source hides it.
    /// </summary>
    Task<CoverDeliveryResult> GetStoredAsync(
        Guid artistId,
        ArtistPictureKind kind,
        int? size,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores an uploaded picture (JPEG, PNG or WebP, recognized by its content) as the uploaded picture of the artist,
    /// cropped to a square around its center, re-encoded without its metadata. Replaces an earlier uploaded picture.
    /// </summary>
    Task<ArtistPictureUploadStatus> UploadAsync(
        Guid artistId,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the stored picture of the kind of the artist. Returns <c>false</c> if the artist does not exist; an
    /// artist without such a picture is fine.
    /// </summary>
    Task<bool> RemoveAsync(Guid artistId, ArtistPictureKind kind, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the files of pictures that no longer exist, e.g. of artists that were removed by a scan.
    /// </summary>
    Task RemoveOrphansAsync(CancellationToken cancellationToken = default);
}
