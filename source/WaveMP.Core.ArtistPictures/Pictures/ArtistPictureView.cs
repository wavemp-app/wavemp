using WaveMP.Data.Projections;

namespace WaveMP.Core.ArtistPictures.Pictures;

/// <summary>
/// What an artist shows: the picture file in its folder, otherwise the uploaded picture, otherwise the online picture,
/// otherwise a collage of the covers of its albums.
/// </summary>
/// <param name="Kind">The shown source.</param>
/// <param name="Version">Version of the shown picture (see <see cref="ArtistPictureVersions"/>); <c>null</c> for the
/// collage and without picture.</param>
/// <param name="AlbumIds">The albums of the collage; empty unless <paramref name="Kind"/> is
/// <see cref="ArtistPictureShownKind.Albums"/>.</param>
public sealed record ArtistPictureView(ArtistPictureShownKind Kind, string? Version, IReadOnlyList<Guid> AlbumIds)
{
    public static ArtistPictureView None { get; } = new(ArtistPictureShownKind.None, null, []);

    /// <summary>
    /// The view of the pictures of an artist; <c>null</c> (pictures not read) shows nothing.
    /// </summary>
    public static ArtistPictureView Of(ArtistPictureRow? picture)
    {
        return picture switch
        {
            { LocalFileId: { } fileId, LocalModifiedAt: { } modifiedAt } => new ArtistPictureView(
                ArtistPictureShownKind.Local,
                ArtistPictureVersions.Local(fileId, modifiedAt),
                []),
            { UploadedPictureId: { } uploadedId } => new ArtistPictureView(
                ArtistPictureShownKind.Uploaded,
                ArtistPictureVersions.Uploaded(uploadedId),
                []),
            { OnlinePicture: { } online } => new ArtistPictureView(
                ArtistPictureShownKind.Online,
                ArtistPictureVersions.Online(online.Id),
                []),
            { CollageAlbumIds.Count: > 0 } => new ArtistPictureView(
                ArtistPictureShownKind.Albums,
                null,
                picture.CollageAlbumIds),
            _ => None,
        };
    }
}
