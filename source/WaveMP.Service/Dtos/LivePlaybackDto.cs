namespace WaveMP.Service.Dtos;

/// <summary>
/// The live playback of the profile as the remotes show it. The queue itself is read with <c>GetQueue</c> when
/// <c>queueVersion</c> changes.
/// </summary>
public sealed class LivePlaybackDto
{
    /// <summary>
    /// Increases with every change; clients ignore states with a lower version than the one they have.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// The version at which the queue (entries, order, shuffle or source) changed last.
    /// </summary>
    public required long QueueVersion { get; init; }

    /// <summary>
    /// The device that plays; null if none was chosen yet. It may be closed: it continues when it comes back.
    /// </summary>
    public required string? ActiveDeviceId { get; init; }

    public required LivePlaybackStatusDto Status { get; init; }

    /// <summary>
    /// Position of the current entry in the queue; -1 without one.
    /// </summary>
    public required int CurrentIndex { get; init; }

    /// <summary>
    /// Id of the song of the current entry; null without one.
    /// </summary>
    public required Guid? CurrentTrackId { get; init; }

    /// <summary>
    /// Number of entries of the queue.
    /// </summary>
    public required int QueueLength { get; init; }

    /// <summary>
    /// Position within the current song in milliseconds when the server sent the state (while playing, the time since
    /// the active device published it is added).
    /// </summary>
    public required int PositionMilliseconds { get; init; }

    /// <summary>
    /// Duration of the current song in milliseconds; 0 if unknown.
    /// </summary>
    public required int DurationMilliseconds { get; init; }

    /// <summary>
    /// Whether the queue is shuffled.
    /// </summary>
    public required bool Shuffle { get; init; }

    public required RepeatModeDto Repeat { get; init; }

    /// <summary>
    /// What the list was started from; null for single songs.
    /// </summary>
    public required PlaybackSourceDto? Source { get; init; }

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

    /// <summary>
    /// Set when this change moved playback to another device; null otherwise.
    /// </summary>
    public required PlaybackHandoverDto? Handover { get; init; }
}
