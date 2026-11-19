using WaveMP.Service.Validators;

namespace WaveMP.Service.Dtos;

/// <summary>
/// An entry of the queue to save.
/// </summary>
public sealed class UpdatePlaybackStateItemDto
{
    /// <summary>
    /// Id of the song. Unknown songs are saved as well and left out when the state is read.
    /// </summary>
    [RequiredValue]
    public required Guid TrackId { get; init; }

    /// <summary>
    /// Whether autoplay added the entry after the list had ended; false if missing.
    /// </summary>
    public bool? Autoplay { get; init; }
}
