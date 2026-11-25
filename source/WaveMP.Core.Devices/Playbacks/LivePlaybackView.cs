using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Devices.Playbacks;

/// <summary>
/// The live playback of a profile as the remotes show it. The queue itself is loaded separately when
/// <paramref name="QueueVersion"/> changes.
/// </summary>
/// <param name="Version">Increases with every change; clients ignore states with a lower version than the one they
/// have.</param>
/// <param name="QueueVersion">The version at which the queue (entries, order, shuffle or source) changed last.</param>
/// <param name="ActiveDeviceId">The device that plays; <c>null</c> if none was chosen yet.</param>
/// <param name="Status">What the player of the active device does.</param>
/// <param name="CurrentIndex">Position of the current entry in the queue; -1 without one.</param>
/// <param name="CurrentTrackId">The song of the current entry; <c>null</c> without one.</param>
/// <param name="QueueLength">Number of entries of the queue.</param>
/// <param name="PositionMilliseconds">Position within the current song at the time the state was sent (while
/// playing, the time since the active device published it is added).</param>
/// <param name="DurationMilliseconds">Duration of the current song; 0 if unknown.</param>
/// <param name="Shuffle">Whether the queue is shuffled.</param>
/// <param name="Repeat">Repeat mode of the player.</param>
/// <param name="Source">What the list was started from; <c>null</c> for single songs.</param>
/// <param name="Volume">Volume from 0 to 1.</param>
/// <param name="Muted">Whether the player is muted.</param>
/// <param name="Crossfade">Whether songs crossfade.</param>
/// <param name="Autoplay">Whether similar songs follow when the queue ends.</param>
/// <param name="Handover">Set when this change moved playback to another device.</param>
public sealed record LivePlaybackView(
    long Version,
    long QueueVersion,
    string? ActiveDeviceId,
    LivePlaybackStatus Status,
    int CurrentIndex,
    Guid? CurrentTrackId,
    int QueueLength,
    int PositionMilliseconds,
    int DurationMilliseconds,
    bool Shuffle,
    RepeatMode Repeat,
    PlaybackSource? Source,
    double Volume,
    bool Muted,
    bool Crossfade,
    bool Autoplay,
    PlaybackHandover? Handover);
