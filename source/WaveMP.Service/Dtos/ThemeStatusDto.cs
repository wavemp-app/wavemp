using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Whether an uploaded theme passes the checks of this version of the server. Installed themes are checked again on
/// every start, because a newer version may reject more in theme packages or no longer support an old theme API
/// version.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ThemeStatusDto>))]
public enum ThemeStatusDto
{
    /// <summary>
    /// The theme passes the checks.
    /// </summary>
    Current,

    /// <summary>
    /// The theme no longer passes the checks; <see cref="ThemeDto.Problems"/> says why. It is still offered as it is,
    /// an admin should upload a new version.
    /// </summary>
    Outdated,
}
