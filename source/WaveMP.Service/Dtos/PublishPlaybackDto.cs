namespace WaveMP.Service.Dtos;

/// <summary>
/// Real-time connection of the devices: the device of the connection publishes the state of its player.
/// </summary>
public sealed class PublishPlaybackDto
{
    /// <summary>
    /// Whether the device takes playback over because the user started it there; it then becomes the active device.
    /// Without it, only the active device may publish. A claim must contain the queue.
    /// </summary>
    public required bool Claim { get; init; }

    /// <summary>
    /// The state of the player.
    /// </summary>
    public required LivePlaybackInputDto Playback { get; init; }
}
