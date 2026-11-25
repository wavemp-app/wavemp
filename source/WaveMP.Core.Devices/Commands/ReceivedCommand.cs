using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Devices.Commands;

/// <summary>
/// A command as the active device receives it: checked by the server, with the songs of commands that play or queue
/// songs. Songs that are not in the library were left out, and the start position follows.
/// </summary>
/// <param name="FromDeviceId">The remote that sent it.</param>
/// <param name="Type">What the device should do.</param>
/// <param name="Version">The version of the live state when the command was passed on.</param>
/// <param name="ExpectedQueueVersion">Commands on entries of the queue: the queue version the remote saw.</param>
/// <param name="PositionMilliseconds"><see cref="DeviceCommandType.Seek"/>: the new position.</param>
/// <param name="Volume"><see cref="DeviceCommandType.SetVolume"/>: the volume from 0 to 1.</param>
/// <param name="Muted"><see cref="DeviceCommandType.SetVolume"/>: mutes or unmutes; <c>null</c> keeps it.</param>
/// <param name="Enabled">Commands that turn something on or off.</param>
/// <param name="Repeat"><see cref="DeviceCommandType.SetRepeat"/>: the repeat mode.</param>
/// <param name="Tracks">Commands that play or queue songs: the songs, in order.</param>
/// <param name="StartIndex"><see cref="DeviceCommandType.PlayTracks"/>: the position in <paramref name="Tracks"/>
/// to start at.</param>
/// <param name="Source"><see cref="DeviceCommandType.PlayTracks"/>: what the list was started from.</param>
/// <param name="Shuffle"><see cref="DeviceCommandType.PlayTracks"/>: shuffles the list; <c>null</c> keeps the
/// shuffle mode.</param>
/// <param name="Index">Commands on entries of the queue: the position of the entry.</param>
/// <param name="ToIndex"><see cref="DeviceCommandType.MoveQueueEntry"/>: the new position of the entry.</param>
public sealed record ReceivedCommand(
    string FromDeviceId,
    DeviceCommandType Type,
    long Version,
    long? ExpectedQueueVersion,
    int? PositionMilliseconds,
    double? Volume,
    bool? Muted,
    bool? Enabled,
    RepeatMode? Repeat,
    IReadOnlyList<TrackRow>? Tracks,
    int? StartIndex,
    PlaybackSource? Source,
    bool? Shuffle,
    int? Index,
    int? ToIndex);
