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
}

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
