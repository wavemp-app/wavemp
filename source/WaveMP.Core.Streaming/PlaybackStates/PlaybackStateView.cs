using WaveMP.Data.Entities;

namespace WaveMP.Core.Streaming.PlaybackStates;

/// <summary>
/// The saved playback state of a user with the tracks of its queue. Entries of tracks that left the library are left
/// out; the positions follow.
/// </summary>
/// <param name="Items">The queue in the order it plays.</param>
/// <param name="CurrentIndex">Position of the current entry in <paramref name="Items"/>; -1 without one.</param>
/// <param name="PositionMilliseconds">Position within the current track; 0 if the saved current track is gone.</param>
/// <param name="Shuffle">Whether the queue is shuffled.</param>
/// <param name="OriginalOrder">While shuffled: the positions in <paramref name="Items"/> in the order before
/// shuffling; empty otherwise.</param>
/// <param name="Repeat">Repeat mode of the player.</param>
/// <param name="Source">What the list was started from; <c>null</c> for single songs.</param>
/// <param name="DeviceId">Id of the device that changed the state last.</param>
/// <param name="DeviceName">Name of that device.</param>
/// <param name="ChangedAt">When the state changed on that device.</param>
public sealed record PlaybackStateView(
    IReadOnlyList<PlaybackStateEntry> Items,
    int CurrentIndex,
    int PositionMilliseconds,
    bool Shuffle,
    IReadOnlyList<int> OriginalOrder,
    RepeatMode Repeat,
    PlaybackSource? Source,
    string DeviceId,
    string DeviceName,
    DateTimeOffset ChangedAt);
