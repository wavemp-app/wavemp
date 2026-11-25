namespace WaveMP.Core.OnlineMetadata.Clients;

/// <summary>
/// Wikimedia Commons for the online lookup of artist pictures, without API key. Requests are sent one after the other
/// and only while the lookup is switched on (<see cref="OnlineGate.ArtistPictures"/>).
/// </summary>
public interface IWikimediaCommonsClient
{
    /// <summary>
    /// The size of the thumbnails for previews; one of the standard widths of Wikimedia.
    /// </summary>
    public const int PreviewWidth = 500;

    /// <summary>
    /// The size of the thumbnail that is taken over; one of the standard widths of Wikimedia.
    /// </summary>
    public const int FullWidth = 1280;

    /// <summary>
    /// The pictures of files with their thumbnail of the width (smaller originals are not enlarged), size, type and
    /// license metadata (<c>GET w/api.php?action=query&amp;prop=imageinfo</c>), in the order of the names. Files
    /// Commons does not know are left out.
    /// </summary>
    /// <param name="fileNames">Names of the files without <c>File:</c>, at most 50.</param>
    /// <param name="thumbnailWidth"><see cref="PreviewWidth"/> or <see cref="FullWidth"/>.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    Task<OnlineResponse<IReadOnlyList<WikimediaImage>>> GetImagesAsync(
        IReadOnlyList<string> fileNames,
        int thumbnailWidth,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a picture of Wikimedia (an address of a thumbnail of <see cref="GetImagesAsync"/>).
    /// </summary>
    Task<OnlineResponse<byte[]>> DownloadAsync(Uri url, CancellationToken cancellationToken = default);
}

/// <summary>
/// A picture on Wikimedia Commons. The metadata are untrusted text as Commons delivers it: <see cref="Artist"/> and
/// <see cref="Credit"/> are HTML and must never be shown as markup.
/// </summary>
/// <param name="FileName">Name of the file without <c>File:</c>.</param>
/// <param name="ThumbnailUrl">Address of the thumbnail of the requested width.</param>
/// <param name="DescriptionUrl">Address of the file page.</param>
/// <param name="Width">Width of the original in pixels.</param>
/// <param name="Height">Height of the original in pixels.</param>
/// <param name="MimeType">Type of the original, e.g. image/jpeg.</param>
/// <param name="Artist">Author as HTML.</param>
/// <param name="Credit">Source as HTML.</param>
/// <param name="LicenseShortName">Short name of the license, e.g. CC BY-SA 4.0.</param>
/// <param name="LicenseUrl">Address of the license text.</param>
/// <param name="AttributionRequired">Whether the license asks for attribution.</param>
public sealed record WikimediaImage(
    string FileName,
    Uri? ThumbnailUrl,
    Uri? DescriptionUrl,
    int? Width,
    int? Height,
    string? MimeType,
    string? Artist,
    string? Credit,
    string? LicenseShortName,
    string? LicenseUrl,
    bool? AttributionRequired);
