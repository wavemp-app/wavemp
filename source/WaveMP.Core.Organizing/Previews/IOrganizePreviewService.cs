using WaveMP.Data.Entities;

namespace WaveMP.Core.Organizing.Previews;

/// <summary>
/// Makes the previews of organize operations: every change of an operation with its conflicts and warnings, stored as
/// an operation in the state <see cref="OrganizeOperationStatus.Previewed"/>. A preview changes no file; it is applied
/// only after the admin confirmed it, and is checked against the disk again then.
/// </summary>
public interface IOrganizePreviewService
{
    /// <summary>
    /// Previews applying an own album: its values into the tags of its songs and its songs (and cover) into the
    /// folders of the layout template; songs taken out since the last apply get their original tags back.
    /// </summary>
    Task<OrganizePreviewResult> PreviewOwnAlbumAsync(
        Guid ownAlbumId,
        Guid? userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Previews moving every song of the library (or of one source) by the layout template; tags stay. Returns the
    /// operation in the state <see cref="OrganizeOperationStatus.Planning"/>; the organize job plans it (its counters
    /// grow meanwhile) and then it is previewed. Busy while the organize job is queued or running.
    /// </summary>
    Task<OrganizePreviewResult> PreviewLibraryAsync(
        Guid? sourceId,
        Guid? userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Plans a library operation in the state <see cref="OrganizeOperationStatus.Planning"/>; ends it as previewed,
    /// failed, or canceled if the admin asked it to stop.
    /// </summary>
    Task PlanLibraryAsync(Guid operationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Previews undoing a completed, stopped, canceled or interrupted operation.
    /// </summary>
    Task<OrganizePreviewResult> PreviewUndoAsync(
        Guid operationId,
        Guid? userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome of a preview.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="Operation">The stored preview, if created.</param>
public sealed record OrganizePreviewResult(OrganizePreviewStatus Status, OrganizeOperation? Operation)
{
    public static OrganizePreviewResult Failed(OrganizePreviewStatus status)
    {
        return new OrganizePreviewResult(status, null);
    }
}

/// <summary>
/// Why a preview was not made.
/// </summary>
public enum OrganizePreviewStatus
{
    Created,
    OwnAlbumNotFound,
    SourceNotFound,
    OperationNotFound,

    /// <summary>
    /// The own album has no songs.
    /// </summary>
    AlbumEmpty,

    /// <summary>
    /// A media folder of the operation cannot be written.
    /// </summary>
    MediaReadOnly,

    /// <summary>
    /// The saved layout template cannot be used.
    /// </summary>
    TemplateInvalid,

    /// <summary>
    /// Another operation is queued or running.
    /// </summary>
    Busy,

    /// <summary>
    /// The operation cannot be undone: it is an undo itself, did not run, or changed nothing.
    /// </summary>
    NotUndoable,

    /// <summary>
    /// The operation was undone already.
    /// </summary>
    AlreadyUndone,
}
