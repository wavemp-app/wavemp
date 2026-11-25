using WaveMP.Core.Streaming.PlaybackStates;

namespace WaveMP.Core.Devices.Playbacks;

/// <summary>
/// The queue of the active device as it publishes it.
/// </summary>
/// <param name="Items">The queue in the order it plays, at most <see cref="Validations.DeviceLimits.MaxQueueItems"/>
/// entries (the entries around the current one of a longer queue).</param>
/// <param name="Shuffle">Whether the queue is shuffled.</param>
/// <param name="OriginalOrder">While shuffled: the positions in <paramref name="Items"/> in the order before
/// shuffling (each position once); may be empty. Ignored while not shuffled.</param>
/// <param name="Source">What the list was started from; <c>null</c> for single songs.</param>
public sealed record LiveQueueInput(
    IReadOnlyList<PlaybackStateItem> Items,
    bool Shuffle,
    IReadOnlyList<int> OriginalOrder,
    PlaybackSource? Source);
