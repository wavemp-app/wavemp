using WaveMP.Data.Entities;

namespace WaveMP.Data.Repositories;

/// <summary>
/// One page of the organize operations, newest first.
/// </summary>
/// <param name="Offset">Number of operations to skip.</param>
/// <param name="Limit">Largest number of operations of the page.</param>
/// <param name="Kind">Only operations of this kind.</param>
/// <param name="Status">Only operations in this state.</param>
public sealed record OrganizeOperationListQuery(
    int Offset,
    int Limit,
    OrganizeOperationKind? Kind = null,
    OrganizeOperationStatus? Status = null);

/// <summary>
/// One page of the items of an organize operation in their order.
/// </summary>
/// <param name="OperationId">The operation.</param>
/// <param name="Offset">Number of items to skip.</param>
/// <param name="Limit">Largest number of items of the page.</param>
/// <param name="Filter">Which items.</param>
public sealed record OrganizeItemListQuery(Guid OperationId, int Offset, int Limit, OrganizeItemFilter Filter);

/// <summary>
/// Which items of an organize operation are listed.
/// </summary>
public enum OrganizeItemFilter
{
    All,
    Conflicts,
    Warnings,
}

/// <summary>
/// Result of discarding a preview.
/// </summary>
public enum OrganizeDiscardStatus
{
    Discarded,
    NotFound,

    /// <summary>
    /// The operation is no preview (it was confirmed or ran).
    /// </summary>
    NotPreview,
}

/// <summary>
/// Result of asking an organize operation to stop.
/// </summary>
public enum OrganizeCancelStatus
{
    /// <summary>
    /// The operation stops before its next item (or before it starts, or before the next part of its plan).
    /// </summary>
    Requested,

    NotFound,

    /// <summary>
    /// The operation is not planning, queued or running.
    /// </summary>
    NotCancelable,
}
