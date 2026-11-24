using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;

namespace WaveMP.Data.Repositories;

/// <summary>
/// Data access of the organization of the media files.
/// </summary>
public interface IOrganizeRepository
{
    /// <summary>
    /// The values of the given tracks for a layout template, in the order of <paramref name="trackIds"/>; unknown ids
    /// are left out.
    /// </summary>
    Task<IReadOnlyList<TemplateSongRow>> GetTemplateSongsAsync(
        IReadOnlyList<Guid> trackIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The values of the first <paramref name="count"/> tracks of the library for a layout template, ordered by album
    /// artist, album, disc, track number and title; tracks with an album come first.
    /// </summary>
    Task<IReadOnlyList<TemplateSongRow>> GetSampleTemplateSongsAsync(
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The songs with these ids with their values and all their files; unknown ids are left out.
    /// </summary>
    Task<IReadOnlyList<OrganizeSongRow>> GetSongsAsync(
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The files of the library with these ids; unknown ids are left out.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, OrganizeFileRow>> GetFilesAsync(
        IReadOnlyCollection<Guid> mediaFileIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The next <paramref name="count"/> song ids of the library (or of one source) after <paramref name="afterId"/>,
    /// ordered by id.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListTrackIdsAsync(
        Guid? sourceId,
        Guid? afterId,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The folders of a source with all their songs and the files that belong to no song.
    /// </summary>
    Task<IReadOnlyList<OrganizeFolderRow>> GetFoldersAsync(
        Guid sourceId,
        IReadOnlyCollection<string> directoryPaths,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The number of songs of the album with this album key that are not among <paramref name="trackIds"/>.
    /// </summary>
    Task<int> CountOtherAlbumTracksAsync(
        string albumKey,
        IReadOnlyCollection<Guid> trackIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The tag backups with these ids.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, TagBackup>> GetTagBackupsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new operation with its items and moves.
    /// </summary>
    Task AddOperationAsync(OrganizeOperation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds items (with their moves) to an operation that is being planned.
    /// </summary>
    Task AddItemsAsync(IReadOnlyList<OrganizeItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the planning of an operation: sets its state, counters and times. Returns <c>false</c> if the operation
    /// is gone or no longer planning.
    /// </summary>
    Task<bool> FinishPlanningAsync(
        Guid id,
        OrganizeOperationStatus status,
        OrganizeErrorCode? errorCode,
        DateTimeOffset previewedAt,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// An operation without its items, or <c>null</c>.
    /// </summary>
    Task<OrganizeOperation?> FindOperationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Page<OrganizeOperation>> ListOperationsAsync(
        OrganizeOperationListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of the items of an operation with their moves.
    /// </summary>
    Task<Page<OrganizeItem>> ListItemsAsync(
        OrganizeItemListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// All items of an operation in a state, with their moves, in their order.
    /// </summary>
    Task<IReadOnlyList<OrganizeItem>> ListItemsByStatusAsync(
        Guid operationId,
        OrganizeItemStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The operation that is queued or running, or <c>null</c>.
    /// </summary>
    Task<Guid?> FindActiveOperationIdAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks previews that were not confirmed in time as expired. Returns their number.
    /// </summary>
    Task<int> ExpirePreviewsAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a preview (planning, previewed or expired) with its items.
    /// </summary>
    Task<OrganizeDiscardStatus> DeletePreviewAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the new place of the files of a done item in one transaction: the paths of its files (also in the file
    /// selections) and the folder and base name of its song, the moves as moved, the item as done and the counter of
    /// the operation. Ids of songs and files stay, so everything that refers to them stays valid.
    /// </summary>
    Task RelocateTrackAsync(TrackRelocation relocation, CancellationToken cancellationToken = default);
}
