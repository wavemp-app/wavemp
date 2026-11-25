namespace WaveMP.Service.Dtos;

/// <summary>
/// The queue of a device as it publishes it.
/// </summary>
public sealed class LiveQueueInputDto
{
    /// <summary>
    /// The queue in the order it plays, at most 2000 entries (the entries around the current one of a longer queue).
    /// </summary>
    public required IReadOnlyList<LiveQueueItemDto> Items { get; init; }

    /// <summary>
    /// Whether the queue is shuffled.
    /// </summary>
    public required bool Shuffle { get; init; }

    /// <summary>
    /// While shuffled: the positions in <c>items</c> in the order before shuffling, each position once; may be empty
    /// or missing. Ignored while not shuffled.
    /// </summary>
    public IReadOnlyList<int>? OriginalOrder { get; init; }

    /// <summary>
    /// What the list was started from; null or missing for single songs.
    /// </summary>
    public PlaybackSourceDto? Source { get; init; }
}
