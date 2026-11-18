namespace WaveMP.Core.Streaming.Histories;

/// <summary>
/// Learns about every new entry of the play history, e.g. to report the play to a scrobbling service. Registered as
/// scoped service; all listeners are called one after the other once the entry is saved. An exception of a listener
/// is logged and does not undo the entry.
/// </summary>
public interface IPlayHistoryListener
{
    /// <summary>
    /// A play was recorded; <see cref="PlayHistoryItem.Entry"/> names the user.
    /// </summary>
    Task PlayRecordedAsync(PlayHistoryItem item, CancellationToken cancellationToken = default);
}
