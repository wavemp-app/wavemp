namespace WaveMP.Service.Dtos;

/// <summary>
/// The devices of the profile after one opened or closed, was renamed or forgotten, allowed or forbade remote control,
/// or the active device changed.
/// </summary>
public sealed class DeviceListDto
{
    /// <summary>
    /// All devices of the profile, open ones first, then the most recently active.
    /// </summary>
    public required IReadOnlyList<DeviceDto> Devices { get; init; }
}
