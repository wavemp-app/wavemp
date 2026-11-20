namespace WaveMP.Core.Recaps.Shares;

/// <summary>
/// Result of taking back a shared recap.
/// </summary>
public enum RecapShareDeletion
{
    Deleted,

    /// <summary>
    /// The share does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    /// Another profile shared the recap; only that profile can take it back.
    /// </summary>
    NotOwner,
}
