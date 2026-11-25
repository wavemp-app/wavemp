namespace WaveMP.Service.Dtos;

/// <summary>
/// A command for this device, sent by a remote of the profile and checked by the server. Only the members its type
/// uses are set; the others are null.
/// </summary>
public sealed class ReceivedCommandDto
{
    /// <summary>
    /// The remote that sent it.
    /// </summary>
    public required string FromDeviceId { get; init; }

    public required DeviceCommandTypeDto Type { get; init; }

    /// <summary>
    /// The version of the live state when the server passed the command on.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// Commands on entries of the queue: the queue version the remote saw (the server checked it).
    /// </summary>
    public required long? ExpectedQueueVersion { get; init; }

    /// <summary>
    /// <c>Seek</c>: the new position in milliseconds.
    /// </summary>
    public required int? PositionMilliseconds { get; init; }

    /// <summary>
    /// <c>SetVolume</c>: the volume from 0 to 1.
    /// </summary>
    public required double? Volume { get; init; }

    /// <summary>
    /// <c>SetVolume</c>: mutes or unmutes; null keeps it.
    /// </summary>
    public required bool? Muted { get; init; }

    /// <summary>
    /// <c>SetShuffle</c>, <c>SetCrossfade</c> and <c>SetAutoplay</c>: on or off.
    /// </summary>
    public required bool? Enabled { get; init; }

    /// <summary>
    /// <c>SetRepeat</c>: the repeat mode.
    /// </summary>
    public required RepeatModeDto? Repeat { get; init; }

    /// <summary>
    /// <c>PlayTracks</c>, <c>AddToQueue</c> and <c>PlayNext</c>: the songs in order, with the favorite and rating of
    /// the profile; songs that are not in the library were left out.
    /// </summary>
    public required IReadOnlyList<TrackDto>? Tracks { get; init; }

    /// <summary>
    /// <c>PlayTracks</c>: the position in <c>tracks</c> to start at.
    /// </summary>
    public required int? StartIndex { get; init; }

    /// <summary>
    /// <c>PlayTracks</c>: what the list was started from.
    /// </summary>
    public required PlaybackSourceDto? Source { get; init; }

    /// <summary>
    /// <c>PlayTracks</c>: shuffles the list; null keeps the shuffle mode of the player.
    /// </summary>
    public required bool? Shuffle { get; init; }

    /// <summary>
    /// Commands on entries of the queue: the position of the entry.
    /// </summary>
    public required int? Index { get; init; }

    /// <summary>
    /// <c>MoveQueueEntry</c>: the new position of the entry.
    /// </summary>
    public required int? ToIndex { get; init; }
}
