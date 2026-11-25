namespace WaveMP.Core.Devices.Devices;

/// <summary>
/// Outcome of forgetting a device.
/// </summary>
public enum DeviceDeleteStatus
{
    /// <summary>
    /// The device is forgotten.
    /// </summary>
    Deleted,

    /// <summary>
    /// The profile has no device with this id.
    /// </summary>
    NotFound,

    /// <summary>
    /// The device is open right now; it would register again at once.
    /// </summary>
    Online,
}
