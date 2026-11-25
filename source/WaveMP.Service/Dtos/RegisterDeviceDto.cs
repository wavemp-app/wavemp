namespace WaveMP.Service.Dtos;

/// <summary>
/// Real-time connection of the devices (asyncapi.yaml): registers the device the connection runs on. The first call
/// of every connection; the other calls answer <c>NotRegistered</c> before it.
/// </summary>
public sealed class RegisterDeviceDto
{
    /// <summary>
    /// Id the device chose for itself and keeps: 1 to 64 letters, digits, dots, underscores, colons and hyphens. A
    /// newer connection with the same id (e.g. another tab of the same browser) replaces the older one.
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Name the device reports for itself, 1 to 100 characters without control characters, e.g. "Firefox · Windows".
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Kind of the device.
    /// </summary>
    public required DeviceTypeDto Type { get; init; }

    /// <summary>
    /// Whether the user allowed remote control on the running page (or app). Never stored: a reconnect of the same
    /// page sends what the page still knows, a reload starts without it.
    /// </summary>
    public required bool RemoteControl { get; init; }
}
