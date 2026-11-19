using WaveMP.Data.Entities;

namespace WaveMP.Core.Streaming.PlaybackStates;

/// <summary>
/// The playback state a device saves for its user.
/// </summary>
/// <param name="Items">The queue in the order it plays, at most <see cref="PlaybackStateLimits.MaxItems"/> entries.
/// </param>
/// <param name="CurrentIndex">Position of the current entry in <paramref name="Items"/>; -1 without one.</param>
/// <param name="PositionMilliseconds">Position within the current track.</param>
/// <param name="Shuffle">Whether the queue is shuffled.</param>
/// <param name="OriginalOrder">While shuffled: the positions in <paramref name="Items"/> in the order before
/// shuffling (each position once); may be empty. Ignored while not shuffled.</param>
/// <param name="Repeat">Repeat mode of the player.</param>
/// <param name="Source">What the list was started from; <c>null</c> for single songs.</param>
/// <param name="DeviceId">Id the device chose for itself.</param>
/// <param name="DeviceName">Name of the device for other devices.</param>
/// <param name="ChangedAt">When the state changed on the device; now if <c>null</c>. A time in the future counts as
/// now.</param>
public sealed record SavePlaybackStateCommand(
    IReadOnlyList<PlaybackStateItem> Items,
    int CurrentIndex,
    int PositionMilliseconds,
    bool Shuffle,
    IReadOnlyList<int> OriginalOrder,
    RepeatMode Repeat,
    PlaybackSource? Source,
    string DeviceId,
    string DeviceName,
    DateTimeOffset? ChangedAt);
