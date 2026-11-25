namespace WaveMP.Service.Dtos;

/// <summary>
/// Result of registering a device on the real-time connection of the devices.
/// </summary>
public sealed class RegisterResultDto
{
    public required RegisterStatusDto Status { get; init; }

    /// <summary>
    /// The devices and the live playback of the profile; null unless registered.
    /// </summary>
    public required DeviceSessionSnapshotDto? Snapshot { get; init; }

    /// <summary>
    /// The rejected fields of an invalid registration; empty otherwise.
    /// </summary>
    public required IReadOnlyList<ValidationErrorDto> Errors { get; init; }
}
