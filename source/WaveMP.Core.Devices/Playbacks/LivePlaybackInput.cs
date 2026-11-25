using WaveMP.Data.Entities;

namespace WaveMP.Core.Devices.Playbacks;

/// <summary>
/// The state of the player of the active device as it publishes it.
/// </summary>
/// <param name="Status">What the player does.</param>
/// <param name="Queue">The queue; <c>null</c> if it did not change since the last publication of the device.</param>
/// <param name="CurrentIndex">Position of the current entry in the queue; -1 without one.</param>
/// <param name="PositionMilliseconds">Position within the current song when the device published it.</param>
/// <param name="DurationMilliseconds">Duration of the current song; 0 if unknown.</param>
/// <param name="Repeat">Repeat mode of the player.</param>
/// <param name="Volume">Volume from 0 to 1.</param>
/// <param name="Muted">Whether the player is muted.</param>
/// <param name="Crossfade">Whether songs crossfade.</param>
/// <param name="Autoplay">Whether similar songs follow when the queue ends.</param>
public sealed record LivePlaybackInput(
    LivePlaybackStatus Status,
    LiveQueueInput? Queue,
    int CurrentIndex,
    int PositionMilliseconds,
    int DurationMilliseconds,
    RepeatMode Repeat,
    double Volume,
    bool Muted,
    bool Crossfade,
    bool Autoplay);
