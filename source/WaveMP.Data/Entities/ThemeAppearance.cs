namespace WaveMP.Data.Entities;

/// <summary>
/// Which variant of a theme with a light and a dark variant a user sees. Stored as its name.
/// </summary>
public enum ThemeAppearance
{
    /// <summary>
    /// The variant follows the light or dark mode of the device; the default.
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
