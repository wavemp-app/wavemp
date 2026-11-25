namespace WaveMP.Core.Devices.Validations;

/// <summary>
/// Why a field of a message of a device was rejected; the same codes as the validation errors of the REST API.
/// </summary>
public enum DeviceValidationCode
{
    /// <summary>
    /// The value is missing, null or empty (also white space only).
    /// </summary>
    Required,

    /// <summary>
    /// The text is longer than <see cref="DeviceValidationError.Max"/> characters.
    /// </summary>
    MaxLength,

    /// <summary>
    /// The list has fewer than <see cref="DeviceValidationError.Min"/> entries.
    /// </summary>
    MinItems,

    /// <summary>
    /// The list has more than <see cref="DeviceValidationError.Max"/> entries.
    /// </summary>
    MaxItems,

    /// <summary>
    /// The number is outside the range from <see cref="DeviceValidationError.Min"/> to
    /// <see cref="DeviceValidationError.Max"/> (both included).
    /// </summary>
    OutOfRange,

    /// <summary>
    /// The value has the wrong form, e.g. a device id with other characters or a name with control characters.
    /// </summary>
    InvalidFormat,

    /// <summary>
    /// The value has the right form but is not allowed, e.g. an undefined enum value or an order before shuffling
    /// that does not name every position once.
    /// </summary>
    InvalidValue,
}
