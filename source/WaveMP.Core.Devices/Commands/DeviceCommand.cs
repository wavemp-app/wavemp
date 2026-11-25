using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Devices.Commands;

/// <summary>
/// A command a remote sends to the active device of its profile. Only the members its type uses are read.
/// </summary>
/// <param name="TargetDeviceId">The device that should act: the active device, which allows remote control.</param>
/// <param name="Type">What it should do.</param>
/// <param name="ExpectedQueueVersion">Commands on entries of the queue: the queue version the remote saw; a queue
/// that changed since then answers <see cref="CommandStatus.Conflict"/>.</param>
/// <param name="PositionMilliseconds"><see cref="DeviceCommandType.Seek"/>: the new position.</param>
/// <param name="Volume"><see cref="DeviceCommandType.SetVolume"/>: the volume from 0 to 1.</param>
/// <param name="Muted"><see cref="DeviceCommandType.SetVolume"/>: mutes or unmutes; <c>null</c> keeps it.</param>
/// <param name="Enabled">Commands that turn something on or off.</param>
/// <param name="Repeat"><see cref="DeviceCommandType.SetRepeat"/>: the repeat mode.</param>
/// <param name="TrackIds">Commands that play or queue songs: the songs, at most
/// <see cref="Validations.DeviceLimits.MaxTrackIds"/>.</param>
/// <param name="StartIndex"><see cref="DeviceCommandType.PlayTracks"/>: the position in
/// <paramref name="TrackIds"/> to start at; 0 if missing.</param>
/// <param name="Source"><see cref="DeviceCommandType.PlayTracks"/>: what the list was started from.</param>
/// <param name="Shuffle"><see cref="DeviceCommandType.PlayTracks"/>: shuffles the list; <c>null</c> keeps the
/// shuffle mode of the player.</param>
/// <param name="Index">Commands on entries of the queue: the position of the entry.</param>
/// <param name="ToIndex"><see cref="DeviceCommandType.MoveQueueEntry"/>: the new position of the entry.</param>
public sealed record DeviceCommand(
    string TargetDeviceId,
    DeviceCommandType Type,
    long? ExpectedQueueVersion = null,
    int? PositionMilliseconds = null,
    double? Volume = null,
    bool? Muted = null,
    bool? Enabled = null,
    RepeatMode? Repeat = null,
    IReadOnlyList<Guid>? TrackIds = null,
    int? StartIndex = null,
    PlaybackSource? Source = null,
    bool? Shuffle = null,
    int? Index = null,
    int? ToIndex = null);
