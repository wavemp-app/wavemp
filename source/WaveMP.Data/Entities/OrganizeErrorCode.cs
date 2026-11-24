namespace WaveMP.Data.Entities;

/// <summary>
/// Why an organize operation failed or stopped. Stored as its name.
/// </summary>
public enum OrganizeErrorCode
{
    /// <summary>
    /// An unexpected error; the log of the server has the details.
    /// </summary>
    Unexpected,

    /// <summary>
    /// An item failed; the item names its conflict.
    /// </summary>
    ItemFailed,

    /// <summary>
    /// The server stopped during the run.
    /// </summary>
    Interrupted,

    /// <summary>
    /// The media folder cannot be written.
    /// </summary>
    MediaReadOnly,
}
