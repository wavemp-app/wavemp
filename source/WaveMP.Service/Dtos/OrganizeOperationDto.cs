namespace WaveMP.Service.Dtos;

/// <summary>
/// An organize operation: a preview, a run or an entry of the log that can be undone.
/// </summary>
public sealed class OrganizeOperationDto
{
    public required Guid Id { get; init; }

    public required OrganizeOperationKindDto Kind { get; init; }

    public required OrganizeOperationStatusDto Status { get; init; }

    /// <summary>
    /// The own album that is applied; null for other kinds and after the album was deleted.
    /// </summary>
    public Guid? OwnAlbumId { get; init; }

    /// <summary>
    /// The media source a library operation is limited to; null for all sources.
    /// </summary>
    public Guid? SourceId { get; init; }

    /// <summary>
    /// The operation an undo operation undoes.
    /// </summary>
    public Guid? UndoOfId { get; init; }

    /// <summary>
    /// The undo operation that undid this operation.
    /// </summary>
    public Guid? UndoneById { get; init; }

    /// <summary>
    /// The layout template of the preview.
    /// </summary>
    public required string Template { get; init; }

    /// <summary>
    /// Name for the log, e.g. the title of the own album at the time of the preview.
    /// </summary>
    public string? DisplayTitle { get; init; }

    /// <summary>
    /// Why the operation failed or stopped.
    /// </summary>
    public OrganizeErrorCodeDto? ErrorCode { get; init; }

    /// <summary>
    /// Whether the run leaves out items with conflicts.
    /// </summary>
    public required bool SkipConflicts { get; init; }

    public required int TotalItems { get; init; }

    public required int DoneItems { get; init; }

    public required int SkippedItems { get; init; }

    public required int FailedItems { get; init; }

    /// <summary>
    /// Number of items with a conflict.
    /// </summary>
    public required int ConflictItems { get; init; }

    /// <summary>
    /// Number of items with at least one warning.
    /// </summary>
    public required int WarningItems { get; init; }

    /// <summary>
    /// Whether the operation can be undone now (UndoOrganizeOperation).
    /// </summary>
    public required bool CanUndo { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// When the preview was ready.
    /// </summary>
    public DateTimeOffset? PreviewedAt { get; init; }

    /// <summary>
    /// Until when the preview can be applied.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    public DateTimeOffset? StartedAt { get; init; }

    public DateTimeOffset? FinishedAt { get; init; }
}
