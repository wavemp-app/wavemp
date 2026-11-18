using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace WaveMP.Core.Themes.Definitions;

/// <summary>
/// The id of an uploaded theme: lower-case letters and digits in words separated by single hyphens, e.g.
/// <c>aurora</c> or <c>deep-sea</c>, the same pattern as the theme ids of the settings of a profile, at most 40
/// characters. It names the folder of the theme and is part of the addresses of its files.
/// </summary>
public static partial class ThemeIdRules
{
    public const int MaxLength = 40;

    public const string Pattern = "^[a-z0-9]+(-[a-z0-9]+)*$";

    public static bool IsValid([NotNullWhen(true)] string? id)
    {
        return id is { Length: > 0 and <= MaxLength } && IdPattern().IsMatch(id);
    }

    [GeneratedRegex(@"\A[a-z0-9]+(-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();
}
