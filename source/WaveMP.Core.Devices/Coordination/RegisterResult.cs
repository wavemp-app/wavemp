using WaveMP.Core.Devices.Validations;

namespace WaveMP.Core.Devices.Coordination;

/// <summary>
/// Outcome of registering a device.
/// </summary>
/// <param name="Snapshot">The devices and the live playback of the profile; <c>null</c> if the registration was
/// invalid.</param>
/// <param name="Errors">The rejected fields of an invalid registration; empty otherwise.</param>
public sealed record RegisterResult(DeviceSessionSnapshot? Snapshot, IReadOnlyList<DeviceValidationError> Errors)
{
    /// <summary>
    /// Whether the device is registered.
    /// </summary>
    public bool IsRegistered => Snapshot is not null;
}
