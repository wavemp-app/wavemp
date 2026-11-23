namespace WaveMP.Core.Themes.Definitions;

/// <summary>
/// The ids of the themes shipped with the web app (<c>assets/web/themes</c>). Uploaded themes cannot use them, so an
/// uploaded theme never hides a shipped one.
/// </summary>
public static class BuiltInThemes
{
    public static IReadOnlySet<string> Ids { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "midnight",
        "meadow",
        "daylight",
        "accent",
        "pinstripe",
        "clear-glass",
        "classic-9x",
        "skinned",
    };
}
