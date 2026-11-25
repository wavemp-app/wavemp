namespace WaveMP.Service.Dtos;

/// <summary>
/// Playback moved to another device. The new active device loads the saved playback state
/// (<c>GET /api/v1/me/playback-state</c>), which the server wrote with the position of the hand-over.
/// </summary>
public sealed class PlaybackHandoverDto
{
    /// <summary>
    /// The device that was active before; null if none was.
    /// </summary>
    public required string? FromDeviceId { get; init; }

    /// <summary>
    /// Whether the new active device starts playing at once; otherwise it loads the state paused.
    /// </summary>
    public required bool Play { get; init; }
}
