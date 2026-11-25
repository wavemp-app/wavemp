namespace WaveMP.Service.Dtos;

/// <summary>
/// Real-time connection of the devices: a command for the active device of the profile. Only the members its type
/// uses are read; the others may be missing.
/// </summary>
public sealed class DeviceCommandDto
{
    /// <summary>
    /// The device that should act: the active device, which must allow remote control.
    /// </summary>
    public required string TargetDeviceId { get; init; }

    public required DeviceCommandTypeDto Type { get; init; }

    /// <summary>
    /// <c>PlayQueueEntry</c>, <c>RemoveQueueEntry</c>, <c>MoveQueueEntry</c> and <c>ClearQueue</c>: the queue version
    /// the remote saw (required); a queue that changed since then answers <c>Conflict</c>.
    /// </summary>
    public long? ExpectedQueueVersion { get; init; }

    /// <summary>
    /// <c>Seek</c>: the new position in milliseconds, 0 to 86400000.
    /// </summary>
    public int? PositionMilliseconds { get; init; }

    /// <summary>
    /// <c>SetVolume</c>: the volume from 0 to 1.
    /// </summary>
    public double? Volume { get; init; }

    /// <summary>
    /// <c>SetVolume</c>: mutes or unmutes; null keeps it.
    /// </summary>
    public bool? Muted { get; init; }

    /// <summary>
    /// <c>SetShuffle</c>, <c>SetCrossfade</c> and <c>SetAutoplay</c>: on or off.
    /// </summary>
    public bool? Enabled { get; init; }

    /// <summary>
    /// <c>SetRepeat</c>: the repeat mode.
    /// </summary>
    public RepeatModeDto? Repeat { get; init; }

    /// <summary>
    /// <c>PlayTracks</c>, <c>AddToQueue</c> and <c>PlayNext</c>: the songs, 1 to 2000. Songs that are not in the
    /// library are left out; if none is left, the command is invalid.
    /// </summary>
    public IReadOnlyList<Guid>? TrackIds { get; init; }

    /// <summary>
    /// <c>PlayTracks</c>: the position in <c>trackIds</c> to start at; 0 if missing.
    /// </summary>
    public int? StartIndex { get; init; }

    /// <summary>
    /// <c>PlayTracks</c>: what the list was started from.
    /// </summary>
    public PlaybackSourceDto? Source { get; init; }

    /// <summary>
    /// <c>PlayTracks</c>: shuffles the list; null keeps the shuffle mode of the player.
    /// </summary>
    public bool? Shuffle { get; init; }

    /// <summary>
    /// <c>PlayQueueEntry</c>, <c>RemoveQueueEntry</c> and <c>MoveQueueEntry</c>: the position of the entry in the
    /// live queue.
    /// </summary>
    public int? Index { get; init; }

    /// <summary>
    /// <c>MoveQueueEntry</c>: the new position of the entry.
    /// </summary>
    public int? ToIndex { get; init; }
}
