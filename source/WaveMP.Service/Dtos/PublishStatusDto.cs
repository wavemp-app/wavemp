using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Outcome of publishing the state of a player.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<PublishStatusDto>))]
public enum PublishStatusDto
{
    /// <summary>
    /// The state is the live state of the profile now.
    /// </summary>
    Accepted,

    /// <summary>
    /// The connection has not registered a device, or a newer connection of the device replaced it.
    /// </summary>
    NotRegistered,

    /// <summary>
    /// Another device is the active device and the device did not claim playback; nothing changed.
    /// </summary>
    NotActive,

    /// <summary>
    /// The state is invalid; the errors name the fields.
    /// </summary>
    Invalid,
}
