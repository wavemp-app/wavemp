using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Organizing.OwnAlbums;

/// <summary>
/// The own albums of the admin: albums put together from songs of the library. Changing an album changes no file;
/// only applying it (an organize operation with preview) writes tags and moves files. A song is a member of at most
/// one own album and needs an audio file, since the album is written into its tags.
/// </summary>
public interface IOwnAlbumService
{
    Task<Page<OwnAlbumItem>> ListAsync(OwnAlbumListQuery query, CancellationToken cancellationToken = default);

    Task<OwnAlbumItem?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<OwnAlbumResult> CreateAsync(
        OwnAlbumValues values,
        Guid? userId,
        CancellationToken cancellationToken = default);

    Task<OwnAlbumResult> UpdateAsync(Guid id, OwnAlbumChanges changes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the definition and its uploaded cover; files and tags stay as they are.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The songs of the album: members by position, then the songs taken out since the last apply. <c>null</c> if the
    /// album does not exist.
    /// </summary>
    Task<Page<OwnAlbumTrackItem>?> ListTracksAsync(
        Guid id,
        int offset,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the members and their order. Songs no longer listed are taken out: removed (and restored by the next
    /// apply) if the album was applied before, otherwise forgotten.
    /// </summary>
    Task<OwnAlbumResult> ReplaceTracksAsync(
        Guid id,
        IReadOnlyList<Guid> trackIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends songs; songs that are members already stay where they are.
    /// </summary>
    Task<OwnAlbumResult> AddTracksAsync(
        Guid id,
        IReadOnlyList<Guid> trackIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes a song out of the album.
    /// </summary>
    Task<OwnAlbumResult> RemoveTrackAsync(Guid id, Guid trackId, CancellationToken cancellationToken = default);

    Task<OwnAlbumResult> SetCoverAsync(Guid id, Stream content, CancellationToken cancellationToken = default);

    Task<OwnAlbumResult> RemoveCoverAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The cover of the album, or <c>null</c> without one; <c>AlbumExists</c> tells whether the album exists.
    /// </summary>
    Task<(bool AlbumExists, OwnAlbumCover? Cover)> OpenCoverAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes stored covers of albums that no longer exist.
    /// </summary>
    Task RemoveOrphanCoversAsync(CancellationToken cancellationToken = default);
}
