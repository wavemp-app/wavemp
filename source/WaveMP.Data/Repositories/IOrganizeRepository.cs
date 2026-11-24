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
    /// Adds items (with their moves) to an operation that is being planned and counts them into its total.
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

    /// <summary>
    /// The media sources of the items of an operation that are pending (or have a conflict).
    /// </summary>
    Task<IReadOnlyList<Guid>> ListItemSourceIdsAsync(Guid operationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a preview: previewed becomes queued, with whether items with conflicts are left out. Returns
    /// <c>false</c> if the operation is gone or no longer previewed.
    /// </summary>
    Task<bool> QueueAsync(Guid id, bool skipConflicts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Takes back a confirmation whose run could not be started: queued becomes previewed again.
    /// </summary>
    Task<bool> UnqueueAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The ids of the operations in a state, oldest first.
    /// </summary>
    Task<IReadOnlyList<Guid>> ListOperationIdsAsync(
        OrganizeOperationStatus status,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the run of a queued operation: it becomes running. Returns <c>false</c> if it is no longer queued.
    /// </summary>
    Task<bool> StartRunAsync(
        Guid id,
        DateTimeOffset startedAt,
        Guid? jobRunId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends the run of an operation with its state; the counters of done, skipped and failed items are counted from
    /// the items.
    /// </summary>
    Task FinishRunAsync(
        Guid id,
        OrganizeOperationStatus status,
        OrganizeErrorCode? errorCode,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks an operation that is planning, queued or running to stop.
    /// </summary>
    Task<OrganizeCancelStatus> RequestCancelAsync(
        Guid id,
        DateTimeOffset requestedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The next <paramref name="count"/> items of an operation after the item with the order
    /// <paramref name="afterSequence"/>, with their moves, in their order.
    /// </summary>
    Task<IReadOnlyList<OrganizeItem>> ListItemsAfterAsync(
        Guid operationId,
        int afterSequence,
        int count,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the items with a conflict as skipped. Returns their number.
    /// </summary>
    Task<int> SkipConflictItemsAsync(
        Guid operationId,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the journal of an item before anything on disk changes: the item is running, with the folders it is
    /// about to create (outermost first).
    /// </summary>
    Task BeginItemAsync(
        Guid itemId,
        IReadOnlyList<string> createdFolders,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the backup of the tags of an item right before they are written and links it to the item. For a member
    /// of an own album, the first backup also becomes the backup of its original tags.
    /// </summary>
    Task AddItemTagBackupAsync(
        Guid itemId,
        TagBackup backup,
        Guid? ownAlbumId,
        Guid? trackId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the fingerprint of a move after the tags of its file were written.
    /// </summary>
    Task UpdateMoveFingerprintAsync(
        Guid moveId,
        long size,
        DateTimeOffset modifiedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ends an item that is not done: failed before anything changed, or rolled back; its moves are pending or rolled
    /// back accordingly.
    /// </summary>
    Task EndItemAsync(
        Guid itemId,
        OrganizeItemStatus status,
        OrganizeConflict? conflict,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the empty folders removed after an item was done.
    /// </summary>
    Task SetRemovedFoldersAsync(
        Guid itemId,
        IReadOnlyList<string> removedFolders,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The items that are running, of any operation, with their moves: after a restart they were interrupted.
    /// </summary>
    Task<IReadOnlyList<OrganizeItem>> ListRunningItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the operations that are planning, queued or running as interrupted, with their counters. Returns their
    /// ids.
    /// </summary>
    Task<IReadOnlyList<Guid>> InterruptActiveAsync(
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that an operation was undone by another one.
    /// </summary>
    Task SetUndoneByAsync(Guid id, Guid undoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets when an own album was applied for the last time: the end of its latest run that changed files and is not
    /// undone, or <c>null</c>.
    /// </summary>
    Task UpdateOwnAlbumAppliedAtAsync(Guid ownAlbumId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes previews that were never applied (previewed, expired, or failed while planning) and were created
    /// before <paramref name="createdBefore"/>. Returns their number.
    /// </summary>
    Task<int> DeleteOldPreviewsAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken = default);
}
