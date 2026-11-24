namespace WaveMP.Service.Dtos;

/// <summary>
/// The picture of an artist: the picture file in its folder (Local), else the picture an admin uploaded (Uploaded),
/// else the picture an admin took over from the online lookup (Online), else a collage of the covers of its albums
/// (Albums), else none.
/// </summary>
public sealed class ArtistPictureDto
{
    public required ArtistPictureKindDto Kind { get; init; }

    /// <summary>
    /// Version of the shown picture with Local, Uploaded and Online, otherwise null. It changes whenever another
    /// picture is shown, so clients can use it as part of their cache key.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Ids of up to four albums with a cover for the collage, only with Albums, otherwise empty: albums with the artist
    /// as album artist first, then the newest first (albums without year last), then by title. Their pictures come
    /// from the cover endpoint of the albums.
    /// </summary>
    public required IReadOnlyList<Guid> AlbumIds { get; init; }

    /// <summary>
    /// Whether the scan found a picture file in the folder of the artist.
    /// </summary>
    public required bool HasLocalPicture { get; init; }

    /// <summary>
    /// Whether an admin uploaded a picture; it is hidden while there is a picture file in the folder of the artist.
    /// </summary>
    public required bool HasUploadedPicture { get; init; }

    /// <summary>
    /// Whether an admin took over an online picture; it is hidden while there is a picture file in the folder of the
    /// artist or an uploaded picture.
    /// </summary>
    public required bool HasOnlinePicture { get; init; }

    /// <summary>
    /// The credit of the online picture whenever one is stored, also while it is hidden.
    /// </summary>
    public ArtistPictureCreditDto? OnlineCredit { get; init; }

    /// <summary>
    /// The MusicBrainz artist an admin chose in the online lookup, if any.
    /// </summary>
    public Guid? MusicBrainzArtistId { get; init; }
}
