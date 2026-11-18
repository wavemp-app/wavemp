using System.Text.Json.Serialization;
using WaveMP.Service.Converters;

namespace WaveMP.Service.Dtos;

/// <summary>
/// A variant of a theme.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ThemeVariantDto>))]
public enum ThemeVariantDto
{
    /// <summary>
    /// The light variant; the theme CSS styles it under <c>:root</c>.
    /// </summary>
    Light,

    /// <summary>
    /// The dark variant; the theme CSS styles it under <c>[data-theme-variant='dark']</c>.
    /// </summary>
    Dark,
}
