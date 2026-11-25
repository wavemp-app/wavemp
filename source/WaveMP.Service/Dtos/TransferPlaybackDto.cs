namespace WaveMP.Service.Dtos;

/// <summary>
/// Real-time connection of the devices: moves playback to another open device of the profile, or to the device of the
/// connection itself ("play here"), at the same position.
/// </summary>
public sealed class TransferPlaybackDto
{
    /// <summary>
    /// The device that plays afterwards; another device must allow remote control.
    /// </summary>
    public required string TargetDeviceId { get; init; }

    /// <summary>
    /// Whether the device starts playing at once; otherwise it loads the state paused.
    /// </summary>
    public required bool Play { get; init; }

    /// <summary>
    /// The version of the live state the device saw; a state that changed since then answers <c>Conflict</c>. Null or
    /// missing skips the check.
    /// </summary>
    public long? ExpectedVersion { get; init; }
}
