namespace WaveMP.Core.Streaming.PlaybackStates;

/// <summary>
/// Limits of a saved playback state.
/// </summary>
public static class PlaybackStateLimits
{
    /// <summary>
    /// Most entries of a saved queue; clients save the entries around the current one of a longer queue.
    /// </summary>
    public const int MaxItems = 2000;
}
