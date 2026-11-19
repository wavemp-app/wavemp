namespace WaveMP.Service.Dtos;

/// <summary>
/// An entry of a saved queue.
/// </summary>
public sealed class PlaybackStateItemDto
{
    public required TrackDto Track { get; init; }

    /// <summary>
    /// Whether autoplay added the entry after the list had ended.
    /// </summary>
    public required bool Autoplay { get; init; }
}
