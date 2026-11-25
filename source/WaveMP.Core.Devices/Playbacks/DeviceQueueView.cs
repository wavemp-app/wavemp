using WaveMP.Core.Streaming.PlaybackStates;

namespace WaveMP.Core.Devices.Playbacks;

/// <summary>
/// The live queue of a profile with its songs.
/// </summary>
/// <param name="Version">The version of the live state when the queue was read.</param>
/// <param name="QueueVersion">The version at which the queue changed last; commands on entries name it.</param>
/// <param name="CurrentIndex">Position of the current entry; -1 without one.</param>
/// <param name="Shuffle">Whether the queue is shuffled.</param>
/// <param name="Source">What the list was started from; <c>null</c> for single songs.</param>
/// <param name="Items">The entries in the order they play.</param>
public sealed record DeviceQueueView(
    long Version,
    long QueueVersion,
    int CurrentIndex,
    bool Shuffle,
    PlaybackSource? Source,
    IReadOnlyList<DeviceQueueEntry> Items);
