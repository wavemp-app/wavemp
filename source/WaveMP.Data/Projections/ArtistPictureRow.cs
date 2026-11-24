namespace WaveMP.Data.Projections;

/// <summary>
/// The pictures of an artist as far as the lists need them: which sources are there, their versions and the credit
/// of the online picture. Which one is shown follows from the priority picture in the artist folder, uploaded picture,
/// online picture, otherwise a collage of the covers of its albums.
/// </summary>
/// <param name="LocalFileId">The picture file in the artist folder, if the scan found one.</param>
/// <param name="LocalModifiedAt">Time of the last change of that file.</param>
/// <param name="UploadedPictureId">Id of the uploaded picture, if there is one.</param>
/// <param name="OnlinePicture">The online picture an admin took over, if there is one.</param>
/// <param name="MusicBrainzArtistId">The MusicBrainz artist an admin chose in the online lookup.</param>
public sealed record ArtistPictureRow(
    Guid? LocalFileId,
    DateTimeOffset? LocalModifiedAt,
    Guid? UploadedPictureId,
    ArtistOnlinePictureRow? OnlinePicture,
    Guid? MusicBrainzArtistId)
{
    /// <summary>
    /// Up to four albums of the artist with a cover for the collage, only for artists without a picture of their own:
    /// albums with the artist as album artist first, then the newest year first (albums without year last), then by
    /// title and id.
    /// </summary>
    public IReadOnlyList<Guid> CollageAlbumIds { get; init; } = [];

    /// <summary>
    /// Whether the artist has a picture of its own (in its folder, uploaded or taken over online).
    /// </summary>
    public bool HasOwnPicture => LocalFileId is not null || UploadedPictureId is not null || OnlinePicture is not null;
}
