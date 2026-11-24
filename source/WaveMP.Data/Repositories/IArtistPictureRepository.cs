using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Data.Repositories;

/// <summary>
/// The stored pictures of the artists (uploaded and taken over online) and the picture in their folders. Nothing is
/// tracked.
/// </summary>
public interface IArtistPictureRepository
{
    /// <summary>
    /// The picture sources of an artist; <c>null</c> if the artist does not exist.
    /// </summary>
    Task<ArtistPictureSources?> FindSourcesAsync(Guid artistId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the picture as the picture of its kind of its artist and removes the earlier one of that kind. If another
    /// picture of the kind was stored at the same moment, the later one wins. <see cref="ArtistPictureReplacement.Saved"/>
    /// is <c>false</c> if the artist does not exist (any more); the ids of the replaced pictures are returned either
    /// way, so their files can be removed.
    /// </summary>
    Task<ArtistPictureReplacement> ReplaceAsync(
        ArtistPicture picture,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the picture of the kind of the artist. <c>null</c> if the artist does not exist; otherwise the id of the
    /// removed picture, if it had one.
    /// </summary>
    Task<ArtistPictureRemoval?> RemoveAsync(
        Guid artistId,
        ArtistPictureKind kind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The ids of the given pictures that still exist.
    /// </summary>
    Task<IReadOnlySet<Guid>> ListExistingIdsAsync(
        IReadOnlyCollection<Guid> pictureIds,
        CancellationToken cancellationToken = default);
}
