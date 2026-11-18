namespace WaveMP.Core.Themes.Definitions;

/// <summary>
/// The versions of the theme API (THEMING.md) that uploaded themes may be written for. A theme names its version in
/// <c>themeApi</c> of its manifest. When a new version of the theme API changes parts that themes use,
/// <see cref="Current"/> grows; <see cref="Oldest"/> only stays behind if older themes still show correctly.
/// </summary>
public static class ThemeApiVersions
{
    /// <summary>
    /// The version of the theme API of this server, the same as in THEMING.md and in <c>data-theme-api</c>.
    /// </summary>
    public const int Current = 5;

    /// <summary>
    /// The oldest version an uploaded theme may be written for.
    /// </summary>
    public const int Oldest = 5;

    public static bool IsSupported(int version)
    {
        return version is >= Oldest and <= Current;
    }
}
