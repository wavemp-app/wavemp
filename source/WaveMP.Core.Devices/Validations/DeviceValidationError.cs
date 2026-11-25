namespace WaveMP.Core.Devices.Validations;

/// <summary>
/// A rejected field of a message of a device.
/// </summary>
/// <param name="Field">The JSON path of the field in the message, e.g. <c>trackIds</c> or
/// <c>playback.queue.items</c>.</param>
/// <param name="Code">Why it was rejected.</param>
/// <param name="Min">The lower limit the code refers to, where it has one.</param>
/// <param name="Max">The upper limit the code refers to, where it has one.</param>
public sealed record DeviceValidationError(
    string Field,
    DeviceValidationCode Code,
    long? Min = null,
    long? Max = null);
