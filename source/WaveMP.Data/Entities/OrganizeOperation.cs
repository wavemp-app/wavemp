namespace WaveMP.Data.Entities;

/// <summary>
/// An organize operation: first a preview of every change (its items), then, after the confirmation of the admin, the
/// run that changes the files, and afterwards the log entry that can be undone. Every change on disk is written into
/// the items and their moves before it happens, so an interrupted run can be put back.
/// </summary>
public class OrganizeOperation
{
    public Guid Id { get; set; }

    public required OrganizeOperationKind Kind { get; set; }

    public required OrganizeOperationStatus Status { get; set; }

    /// <summary>
    /// The own album that is applied (<see cref="OrganizeOperationKind.OwnAlbum"/>); <c>null</c> otherwise and after
    /// the album was deleted.
    /// </summary>
    public Guid? OwnAlbumId { get; set; }

    public OwnAlbum? OwnAlbum { get; set; }

    /// <summary>
    /// The media source a library operation is limited to; <c>null</c> for all sources.
    /// </summary>
    public Guid? SourceId { get; set; }

    /// <summary>
    /// The operation an undo operation undoes.
    /// </summary>
    public Guid? UndoOfId { get; set; }

    public OrganizeOperation? UndoOf { get; set; }

    /// <summary>
    /// The undo operation that undid this operation; <c>null</c> while it is not undone.
    /// </summary>
    public Guid? UndoneById { get; set; }

    /// <summary>
    /// The layout template of the preview.
    /// </summary>
    public required string Template { get; set; }

    /// <summary>
    /// Name shown in the log, e.g. the title of the own album at the time of the preview.
    /// </summary>
    public string? DisplayTitle { get; set; }

    public OrganizeErrorCode? ErrorCode { get; set; }

    /// <summary>
    /// Whether the run leaves out items with conflicts; set when the admin confirms.
    /// </summary>
    public bool SkipConflicts { get; set; }

    public int TotalItems { get; set; }

    public int DoneItems { get; set; }

    public int SkippedItems { get; set; }

    public int FailedItems { get; set; }

    public int ConflictItems { get; set; }

    public int WarningItems { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public User? CreatedBy { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the preview was ready.
    /// </summary>
    public DateTimeOffset? PreviewedAt { get; set; }

    /// <summary>
    /// Until when the preview can be applied.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public DateTimeOffset? CancelRequestedAt { get; set; }

    /// <summary>
    /// The job run that ran the operation.
    /// </summary>
    public Guid? JobRunId { get; set; }

    public ICollection<OrganizeItem> Items { get; set; } = [];
}
