using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Which variant of a theme with a light and a dark variant the profile sees. Themes with one variant ignore it.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ThemeAppearanceDto>))]
public enum ThemeAppearanceDto
{
    /// <summary>
    /// The variant follows the light or dark mode of the device.
    /// </summary>
    System,

    /// <summary>
    /// Always the light variant.
    /// </summary>
    Light,

    /// <summary>
    /// Always the dark variant.
    /// </summary>
    Dark,
}
