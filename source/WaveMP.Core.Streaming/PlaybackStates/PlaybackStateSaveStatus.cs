namespace WaveMP.Core.Streaming.PlaybackStates;

/// <summary>
/// Outcome of saving a playback state.
/// </summary>
public enum PlaybackStateSaveStatus
{
    /// <summary>
    /// The state is saved.
    /// </summary>
    Saved,

    /// <summary>
    /// Another device saved a state that changed later; it stays.
    /// </summary>
    Outdated,

    /// <summary>
    /// The current index lies outside the queue.
    /// </summary>
    InvalidCurrentIndex,

    /// <summary>
    /// The order before shuffling does not name every position of the queue exactly once.
    /// </summary>
    InvalidOriginalOrder,
}
