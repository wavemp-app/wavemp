using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Whether the profile is connected to a scrobbling service.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ScrobbleConnectionStatusDto>))]
public enum ScrobbleConnectionStatusDto
{
    /// <summary>
    /// The profile has not connected the service.
    /// </summary>
    NotConnected,

    /// <summary>
    /// Played songs are reported to the service.
    /// </summary>
    Connected,

    /// <summary>
    /// The service rejected the stored credentials (e.g. the access was revoked); the profile has to connect the
    /// service again. Scrobbles are kept until then.
    /// </summary>
    AuthorizationFailed,
}
