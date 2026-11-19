namespace WaveMP.Service.Dtos;

/// <summary>
/// Where the current profile stopped listening: the queue, its current entry and position, shuffle and repeat, where
/// the list came from and the device that changed it last. Entries of songs that left the library are left out.
/// </summary>
public sealed class PlaybackStateDto
{
    /// <summary>
    /// The queue in the order it plays, at most 2000 entries; the same song may appear more than once.
    /// </summary>
    public required IReadOnlyList<PlaybackStateItemDto> Items { get; init; }

    /// <summary>
    /// Position of the current entry in <c>items</c>; -1 without one.
    /// </summary>
    public required int CurrentIndex { get; init; }

    /// <summary>
    /// Position within the current song in milliseconds; 0 if the saved current song left the library and the next
    /// entry took its place.
    /// </summary>
    public required int PositionMilliseconds { get; init; }

    /// <summary>
    /// Whether the queue is shuffled.
    /// </summary>
    public required bool Shuffle { get; init; }

    /// <summary>
    /// While shuffled: the positions in <c>items</c> in the order before shuffling, so turning shuffle off restores
    /// it; empty otherwise.
    /// </summary>
    public required IReadOnlyList<int> OriginalOrder { get; init; }

    public required RepeatModeDto Repeat { get; init; }

    /// <summary>
    /// What the list was started from; null for a list of single songs.
    /// </summary>
    public PlaybackSourceDto? Source { get; init; }

    /// <summary>
    /// The device that changed the state last.
    /// </summary>
    public required PlaybackDeviceDto Device { get; init; }

    /// <summary>
    /// When the state changed on that device.
    /// </summary>
    public required DateTimeOffset ChangedAt { get; init; }
}
