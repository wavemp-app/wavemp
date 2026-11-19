using System.ComponentModel.DataAnnotations;
using WaveMP.Core.Streaming.PlaybackStates;

namespace WaveMP.Service.Dtos;

/// <summary>
/// The playback state of a device to save for the current profile; it replaces the saved one.
/// </summary>
public sealed class UpdatePlaybackStateDto
{
    /// <summary>
    /// The queue in the order it plays, at most 2000 entries; of a longer queue the entries around the current one.
    /// </summary>
    [Required]
    [MaxLength(PlaybackStateLimits.MaxItems)]
    public required IReadOnlyList<UpdatePlaybackStateItemDto> Items { get; init; }

    /// <summary>
    /// Position of the current entry in <c>items</c>; -1 without one.
    /// </summary>
    [Required]
    [Range(-1, PlaybackStateLimits.MaxItems - 1)]
    public required int? CurrentIndex { get; init; }

    /// <summary>
    /// Position within the current song in milliseconds.
    /// </summary>
    [Required]
    [Range(0, int.MaxValue)]
    public required int? PositionMilliseconds { get; init; }

    /// <summary>
    /// Whether the queue is shuffled; false if missing.
    /// </summary>
    public bool? Shuffle { get; init; }

    /// <summary>
    /// While shuffled: the positions in <c>items</c> in the order before shuffling, each position exactly once, so
    /// turning shuffle off restores it. Ignored while not shuffled.
    /// </summary>
    [MaxLength(PlaybackStateLimits.MaxItems)]
    public IReadOnlyList<int>? OriginalOrder { get; init; }

    /// <summary>
    /// Repeat mode; Off if missing.
    /// </summary>
    public RepeatModeDto? Repeat { get; init; }

    /// <summary>
    /// What the list was started from; missing for a list of single songs.
    /// </summary>
    public PlaybackSourceDto? Source { get; init; }

    /// <summary>
    /// The device that saves the state.
    /// </summary>
    [Required]
    public required PlaybackDeviceDto Device { get; init; }

    /// <summary>
    /// When the state changed on the device; now if missing. A time in the future counts as now. A state that
    /// changed before the saved one is rejected.
    /// </summary>
    public DateTimeOffset? ChangedAt { get; init; }
}
