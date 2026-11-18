using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Users.Settings;

/// <summary>
/// Ids of the themes of the web app, e.g. <c>midnight</c> or <c>clear-glass</c>. The server keeps the id a client
/// chose without knowing the themes: the clients ship the built-in themes and fall back to the default theme for an
/// id they do not know. The ids are not a closed list, so that themes added later (also uploaded ones) need no
/// change of the API.
/// </summary>
public static partial class ThemeIds
{
    /// <summary>
    /// Theme of new profiles.
    /// </summary>
    public const string Default = UserSettings.DefaultThemeId;

    /// <summary>
    /// Longest id.
    /// </summary>
    public const int MaxLength = UserSettings.ThemeIdMaxLength;

    /// <summary>
    /// Pattern of a valid id for the validation of requests: lower-case letters and digits in words separated by single
    /// hyphens, e.g. <c>clear-glass</c>.
    /// </summary>
    public const string Pattern = "^[a-z0-9]+(-[a-z0-9]+)*$";

    public static bool IsValid([NotNullWhen(true)] string? id)
    {
        // The whole text has to match: "$" of the pattern also matches before a final line break.
        return id is { Length: > 0 and <= MaxLength } && IdPattern().Match(id) is { Success: true } match &&
            match.Length == id.Length;
    }

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}
