namespace WaveMP.Data.Entities;

/// <summary>
/// State of an item of an organize operation. Stored as its name.
/// </summary>
public enum OrganizeItemStatus
{
    /// <summary>
    /// Waits for its run.
    /// </summary>
    Pending,

    /// <summary>
    /// Cannot run, see <see cref="OrganizeItem.Conflict"/>.
    /// </summary>
    Conflict,

    /// <summary>
    /// Being changed right now; the journal of its moves is written.
    /// </summary>
    Running,

    Done,

    /// <summary>
    /// Left out because of its conflict.
    /// </summary>
    Skipped,

    /// <summary>
    /// Failed before anything was changed.
    /// </summary>
    Failed,

    /// <summary>
    /// Failed or interrupted while it ran; everything it changed was put back.
    /// </summary>
    RolledBack,
}
