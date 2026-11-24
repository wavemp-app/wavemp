namespace WaveMP.Data.Entities;

/// <summary>
/// State of an organize operation. Stored as its name.
/// </summary>
public enum OrganizeOperationStatus
{
    /// <summary>
    /// The preview is being computed.
    /// </summary>
    Planning,

    /// <summary>
    /// The preview is ready and waits for the confirmation of the admin; nothing was changed yet.
    /// </summary>
    Previewed,

    /// <summary>
    /// Confirmed; waits for its run.
    /// </summary>
    Queued,

    /// <summary>
    /// Files are being moved.
    /// </summary>
    Running,

    /// <summary>
    /// Every item ran (items with conflicts may have been skipped).
    /// </summary>
    Completed,

    /// <summary>
    /// Stopped at an item that failed; the items before it are done and can be undone.
    /// </summary>
    Stopped,

    /// <summary>
    /// Canceled by the admin between two items; the items before are done and can be undone.
    /// </summary>
    Canceled,

    /// <summary>
    /// Failed as a whole, e.g. while computing the preview; nothing was changed.
    /// </summary>
    Failed,

    /// <summary>
    /// The server stopped during the run; the interrupted item was rolled back on the next start.
    /// </summary>
    Interrupted,

    /// <summary>
    /// The preview was not confirmed in time and can no longer be applied.
    /// </summary>
    Expired,
}
