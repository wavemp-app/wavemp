namespace WaveMP.Service.Dtos;

/// <summary>
/// The live queue of the profile with its songs. Remotes read it when the queue version changes.
/// </summary>
public sealed class DeviceQueueDto
{
    /// <summary>
    /// The version of the live state when the queue was read.
    /// </summary>
    public required long Version { get; init; }

    /// <summary>
    /// The version at which the queue changed last; commands on entries name it as <c>expectedQueueVersion</c>.
    /// </summary>
    public required long QueueVersion { get; init; }

    /// <summary>
    /// Position of the current entry; -1 without one.
    /// </summary>
    public required int CurrentIndex { get; init; }

    /// <summary>
    /// Whether the queue is shuffled.
    /// </summary>
    public required bool Shuffle { get; init; }

    /// <summary>
    /// What the list was started from; null for single songs.
    /// </summary>
    public required PlaybackSourceDto? Source { get; init; }

    /// <summary>
    /// The entries in the order they play, at most 2000.
    /// </summary>
    public required IReadOnlyList<DeviceQueueEntryDto> Items { get; init; }
}
