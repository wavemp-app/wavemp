using System.Text.RegularExpressions;

namespace WaveMP.Core.Devices.Validations;

/// <summary>
/// The rules of the ids and names of devices.
/// </summary>
public static partial class DeviceRules
{
    /// <summary>
    /// The pattern of a device id: 1 to 64 letters, digits, dots, underscores, colons and hyphens (a UUID fits). Also
    /// the route constraint of the REST API.
    /// </summary>
    public const string IdPattern = "^[A-Za-z0-9._:-]{1,64}$";

    /// <summary>
    /// Whether <paramref name="deviceId"/> is a valid device id.
    /// </summary>
    public static bool IsValidId(string? deviceId)
    {
        return deviceId is not null && IdRegex().IsMatch(deviceId);
    }

    /// <summary>
    /// Checks the id of a device in <paramref name="field"/>; <c>null</c> if it is valid.
    /// </summary>
    public static DeviceValidationError? ValidateId(string? deviceId, string field)
    {
        if (string.IsNullOrEmpty(deviceId))
        {
            return new DeviceValidationError(field, DeviceValidationCode.Required);
        }

        return IsValidId(deviceId) ? null : new DeviceValidationError(field, DeviceValidationCode.InvalidFormat);
    }

    /// <summary>
    /// The name without spaces at its ends.
    /// </summary>
    public static string NormalizeName(string name)
    {
        return name.Trim();
    }

    /// <summary>
    /// Checks a name of a device in <paramref name="field"/> after removing the spaces at its ends: 1 to 100
    /// characters without control characters. Returns <c>null</c> if it is valid.
    /// </summary>
    public static DeviceValidationError? ValidateName(string? name, string field)
    {
        string normalized = name is null ? string.Empty : NormalizeName(name);
        if (normalized.Length == 0)
        {
            return new DeviceValidationError(field, DeviceValidationCode.Required);
        }

        if (normalized.Length > DeviceLimits.NameMaxLength)
        {
            return new DeviceValidationError(field, DeviceValidationCode.MaxLength, Max: DeviceLimits.NameMaxLength);
        }

        return normalized.Any(char.IsControl)
            ? new DeviceValidationError(field, DeviceValidationCode.InvalidFormat)
            : null;
    }

    // \A and \z instead of ^ and $: $ also matches before a final line break.
    [GeneratedRegex(@"\A[A-Za-z0-9._:-]{1,64}\z", RegexOptions.CultureInvariant)]
    private static partial Regex IdRegex();
}
