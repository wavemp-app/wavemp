namespace WaveMP.Service.Dtos;

/// <summary>
/// What a device gets when it registers: all devices of its profile and the live playback.
/// </summary>
public sealed class DeviceSessionSnapshotDto
{
    /// <summary>
    /// The id the device registered with.
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// All devices of the profile, open ones first, then the most recently active.
    /// </summary>
    public required IReadOnlyList<DeviceDto> Devices { get; init; }

    /// <summary>
    /// The live playback of the profile with its version.
    /// </summary>
    public required LivePlaybackDto Playback { get; init; }
}
