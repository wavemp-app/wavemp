namespace WaveMP.Core.ArtistPictures.Stores;

/// <summary>
/// The stored artist pictures in the folder <c>artist-pictures</c> of the data folder: one folder per picture, named
/// after the id of the picture (never after the artist), with the picture as square WebP files of 96, 300 and 1000
/// pixels (never enlarged). The original file is not kept. Unlike the cache, the folder holds data of the admins and
/// belongs into backups.
/// </summary>
public interface IArtistPictureStore
{
    /// <summary>
    /// Whether pictures can be stored; <c>false</c> if the folder cannot be written.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Creates the folder and removes unfinished files. A folder that cannot be written does not fail; the reason is
    /// logged and new pictures are refused.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recognizes the picture by its content (JPEG, PNG or WebP), crops it to a square around its center, removes its
    /// metadata and stores it in every size under the id of a new picture.
    /// </summary>
    Task<ArtistPictureSaveResult> SaveAsync(
        Guid pictureId,
        Stream content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the stored picture of the size (see <see cref="Caching.Covers.CoverSizes.SnapStored"/>), or <c>null</c>
    /// if there is none. The caller disposes it.
    /// </summary>
    Stream? Open(Guid pictureId, int size);

    /// <summary>
    /// Removes the files of a picture; a picture without files is fine.
    /// </summary>
    void Delete(Guid pictureId);

    /// <summary>
    /// The ids of the pictures that have a folder in the store.
    /// </summary>
    IReadOnlyList<Guid> ListPictureIds();
}
