namespace WaveMP.Service.Dtos;

/// <summary>
/// An entry of the queue a device publishes.
/// </summary>
public sealed class LiveQueueItemDto
{
    /// <summary>
    /// Id of the song.
    /// </summary>
    public required Guid TrackId { get; init; }

    /// <summary>
    /// Whether autoplay added the entry after the list had ended.
    /// </summary>
    public required bool Autoplay { get; init; }
}
