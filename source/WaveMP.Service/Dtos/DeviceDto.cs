namespace WaveMP.Service.Dtos;

/// <summary>
/// A device the current profile has used WaveMP on, with its live state. The same schema is used by the real-time
/// connection of the devices (asyncapi.yaml).
/// </summary>
public sealed class DeviceDto
{
    /// <summary>
    /// Id the device chose for itself and keeps: 1 to 64 letters, digits, dots, underscores, colons and hyphens.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The name to show: the one the user gave the device, otherwise the one it reports.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The name the device reports for itself, e.g. "Firefox · Windows".
    /// </summary>
    public required string ReportedName { get; init; }

    /// <summary>
    /// The name the user gave the device; null if none.
    /// </summary>
    public required string? CustomName { get; init; }

    /// <summary>
    /// Kind of the device.
    /// </summary>
    public required DeviceTypeDto Type { get; init; }

    /// <summary>
    /// Whether the device is open and connected right now.
    /// </summary>
    public required bool Online { get; init; }

    /// <summary>
    /// Whether the device allows remote control right now. The permission lasts as long as the page (or app) runs
    /// on the device; a closed device never allows it.
    /// </summary>
    public required bool RemoteControl { get; init; }

    /// <summary>
    /// Whether the device is the active playback device of the profile. A closed device stays active, so it continues
    /// when it comes back, unless another device takes over.
    /// </summary>
    public required bool Active { get; init; }

    /// <summary>
    /// When the device was last active: opened, played, sent a command or closed.
    /// </summary>
    public required DateTimeOffset LastActiveAt { get; init; }
}
