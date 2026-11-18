namespace WaveMP.Service.Dtos;

/// <summary>
/// Theme of the web app of the current profile.
/// </summary>
public sealed class ThemeSettingsDto
{
    /// <summary>
    /// Id of the theme, e.g. "midnight" or "clear-glass"; "midnight" by default. The ids are not a closed list: the
    /// clients know their themes and show their default theme for an id they do not know.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Which variant of a theme with a light and a dark variant the profile sees; System by default, which follows
    /// the light or dark mode of the device.
    /// </summary>
    public required ThemeAppearanceDto Appearance { get; init; }
}
