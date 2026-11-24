using WaveMP.Data.Entities;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Organizing.Operations;

/// <summary>
/// The organize operations as log: previews, runs and undos with their items, whether the media folders can be
/// organized at all, and discarding a preview. Previews that were not confirmed in time are shown as expired.
/// </summary>
public interface IOrganizeService
{
    Task<Page<OrganizeOperation>> ListOperationsAsync(
        OrganizeOperationListQuery query,
        CancellationToken cancellationToken = default);

    Task<OrganizeOperation?> GetOperationAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// One page of the items of an operation, or <c>null</c> if the operation does not exist.
    /// </summary>
    Task<Page<OrganizeItem>?> ListItemsAsync(
        OrganizeItemListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a preview that was not applied; nothing on disk changes.
    /// </summary>
    Task<OrganizeDiscardStatus> DiscardAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether each media source can be organized, and the operation that is queued or running.
    /// </summary>
    Task<OrganizeAvailability> GetAvailabilityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirms a preview and starts its run by the organize job. The preview must still be valid: not expired, made
    /// with the current layout template, its own album unchanged since, its undo not overtaken, its media folders
    /// writable, and without conflicts unless they are skipped. The run checks every item against the disk again.
    /// </summary>
    /// <param name="id">The operation in the state previewed.</param>
    /// <param name="skipConflicts">Whether items with conflicts are left out.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    Task<OrganizeApplyResult> ApplyAsync(Guid id, bool skipConflicts, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks an operation that is planning, queued or running to stop: between two items, never in the middle of one.
    /// Done items stay done and can be undone.
    /// </summary>
    Task<OrganizeCancelResult> CancelAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// The outcome of confirming a preview.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="Operation">The queued operation.</param>
public sealed record OrganizeApplyResult(OrganizeApplyStatus Status, OrganizeOperation? Operation)
{
    public static OrganizeApplyResult Failed(OrganizeApplyStatus status)
    {
        return new OrganizeApplyResult(status, null);
    }
}

public enum OrganizeApplyStatus
{
    Queued,
    NotFound,

    /// <summary>
    /// The operation is no preview that waits for its confirmation.
    /// </summary>
    NotPreviewed,

    /// <summary>
    /// The preview was not confirmed in time.
    /// </summary>
    PreviewExpired,

    /// <summary>
    /// The preview has conflicts and they are not skipped.
    /// </summary>
    Conflicts,

    /// <summary>
    /// The layout template, the own album or the undone operation changed since the preview.
    /// </summary>
    PreviewOutdated,

    /// <summary>
    /// A media folder of the operation cannot be written.
    /// </summary>
    MediaReadOnly,

    /// <summary>
    /// Another operation is queued or running, or the organize job is busy.
    /// </summary>
    Busy,
}

/// <summary>
/// The outcome of asking an operation to stop.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="Operation">The operation, if it exists.</param>
public sealed record OrganizeCancelResult(OrganizeCancelStatus Status, OrganizeOperation? Operation);

/// <summary>
/// Whether the media sources can be organized.
/// </summary>
/// <param name="Sources">Every media source.</param>
/// <param name="ActiveOperationId">The operation that is queued or running, or <c>null</c>.</param>
public sealed record OrganizeAvailability(IReadOnlyList<SourceAvailability> Sources, Guid? ActiveOperationId);

/// <summary>
/// Whether a media source can be organized.
/// </summary>
/// <param name="SourceId">The source.</param>
/// <param name="Name">Its name.</param>
/// <param name="Status">Whether its files can be moved.</param>
public sealed record SourceAvailability(Guid SourceId, string Name, SourceAvailabilityStatus Status);

public enum SourceAvailabilityStatus
{
    Available,

    /// <summary>
    /// The folder cannot be written, e.g. a read-only mount.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// The folder does not exist.
    /// </summary>
    Missing,

    /// <summary>
    /// The source is switched off.
    /// </summary>
    Inactive,
}
