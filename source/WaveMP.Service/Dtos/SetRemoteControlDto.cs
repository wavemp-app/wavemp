namespace WaveMP.Service.Dtos;

/// <summary>
/// Real-time connection of the devices: the device of the connection allows or forbids remote control, for the
/// running page (or app) only.
/// </summary>
public sealed class SetRemoteControlDto
{
    /// <summary>
    /// Whether other devices of the profile may control this device and take its playback over.
    /// </summary>
    public required bool Allowed { get; init; }
}
