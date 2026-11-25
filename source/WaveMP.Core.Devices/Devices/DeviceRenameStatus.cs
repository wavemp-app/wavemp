namespace WaveMP.Core.Devices.Devices;

/// <summary>
/// Outcome of renaming a device.
/// </summary>
public enum DeviceRenameStatus
{
    /// <summary>
    /// The device has its new name.
    /// </summary>
    Renamed,

    /// <summary>
    /// The profile has no device with this id.
    /// </summary>
    NotFound,

    /// <summary>
    /// The name is invalid; the error names the reason.
    /// </summary>
    Invalid,
}
