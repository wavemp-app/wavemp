using System.ComponentModel.DataAnnotations;
using WaveMP.Core.Users.Settings;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Changes the theme of the current profile. Missing or null values stay unchanged.
/// </summary>
public sealed class UpdateThemeSettingsDto
{
    /// <summary>
    /// Id of the theme, up to 64 lower-case letters and digits in words separated by single hyphens, e.g.
    /// "clear-glass". The server keeps any valid id; the clients offer the themes they know.
    /// </summary>
    [MinLength(1)]
    [MaxLength(ThemeIds.MaxLength)]
    [RegularExpression(ThemeIds.Pattern)]
    public string? Id { get; init; }

    /// <summary>
    /// Which variant of a theme with a light and a dark variant the profile sees.
    /// </summary>
    public ThemeAppearanceDto? Appearance { get; init; }
}
