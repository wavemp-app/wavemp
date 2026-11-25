using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Outcome of registering a device on the real-time connection of the devices.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<RegisterStatusDto>))]
public enum RegisterStatusDto
{
    /// <summary>
    /// The device is registered; the snapshot holds the devices and the live playback of the profile.
    /// </summary>
    Registered,

    /// <summary>
    /// The registration is invalid; the errors name the fields.
    /// </summary>
    Invalid,
}
