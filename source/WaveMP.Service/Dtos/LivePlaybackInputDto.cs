namespace WaveMP.Service.Dtos;

/// <summary>
/// The state of the player of a device as it publishes it.
/// </summary>
public sealed class LivePlaybackInputDto
{
    public required LivePlaybackStatusDto Status { get; init; }

    /// <summary>
    /// The queue; null or missing if it did not change since the last publication of the device.
    /// </summary>
    public LiveQueueInputDto? Queue { get; init; }

    /// <summary>
    /// Position of the current entry in the queue; -1 without one.
    /// </summary>
    public required int CurrentIndex { get; init; }

    /// <summary>
    /// Position within the current song in milliseconds when the device published it, 0 to 86400000.
    /// </summary>
    public required int PositionMilliseconds { get; init; }

    /// <summary>
    /// Duration of the current song in milliseconds, 0 to 86400000; 0 if unknown.
    /// </summary>
    public required int DurationMilliseconds { get; init; }

    public required RepeatModeDto Repeat { get; init; }

    /// <summary>
    /// Volume from 0 to 1.
    /// </summary>
    public required double Volume { get; init; }

    public required bool Muted { get; init; }

    /// <summary>
    /// Whether songs crossfade.
    /// </summary>
    public required bool Crossfade { get; init; }

    /// <summary>
    /// Whether similar songs follow when the queue ends.
    /// </summary>
    public required bool Autoplay { get; init; }
}
