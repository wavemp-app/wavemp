using WaveMP.Core.Devices.Validations;

namespace WaveMP.Core.Devices.Devices;

/// <summary>
/// Outcome of renaming a device.
/// </summary>
/// <param name="Status">What happened.</param>
/// <param name="Device">The renamed device; <c>null</c> otherwise.</param>
/// <param name="Error">Why the name is invalid; <c>null</c> otherwise.</param>
public sealed record DeviceRenameResult(DeviceRenameStatus Status, DeviceView? Device, DeviceValidationError? Error);
