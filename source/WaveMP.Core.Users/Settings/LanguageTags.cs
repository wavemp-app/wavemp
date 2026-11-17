using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Users.Settings;

/// <summary>
/// Languages of the user interface as BCP 47 language tags, e.g. <c>en</c>, <c>pt-BR</c> or <c>zh-Hans</c>. The
/// server keeps the tag a client chose without knowing its translations: every client offers the languages it has
/// translations for and falls back to English for the others.
/// </summary>
public static partial class LanguageTags
{
    /// <summary>
    /// Language of new profiles.
    /// </summary>
    public const string Default = UserSettings.DefaultLanguage;

    /// <summary>
    /// Shortest tag, a language of two letters.
    /// </summary>
    public const int MinLength = 2;

    /// <summary>
    /// Longest tag; the minimum buffer RFC 5646 recommends.
    /// </summary>
    public const int MaxLength = UserSettings.LanguageMaxLength;

    /// <summary>
    /// Pattern of a valid tag for the validation of requests: a language of two or three letters, optionally a script
    /// of four letters, a region of two letters or three digits and variants, separated by hyphens. Extensions and
    /// private use subtags are not needed for the languages of a user interface.
    /// </summary>
    public const string Pattern =
        "^[A-Za-z]{2,3}(-[A-Za-z]{4})?(-([A-Za-z]{2}|[0-9]{3}))?(-([A-Za-z0-9]{5,8}|[0-9][A-Za-z0-9]{3}))*$";

    public static bool IsValid([NotNullWhen(true)] string? tag)
    {
        // The whole text has to match: "$" of the pattern also matches before a final line break.
        return tag is { Length: >= MinLength and <= MaxLength } && TagPattern().Match(tag) is { } match &&
            match.Success && match.Length == tag.Length;
    }

    /// <summary>
    /// The tag in the usual case of its subtags: the language in lower case, the script with an upper-case initial,
    /// the region in upper case and variants in lower case, e.g. <c>PT-br</c> becomes <c>pt-BR</c>.
    /// </summary>
    /// <exception cref="ArgumentException">The tag is not valid, see <see cref="IsValid"/>.</exception>
    public static string Normalize(string tag)
    {
        if (!IsValid(tag))
        {
            throw new ArgumentException("The language tag is not valid.", nameof(tag));
        }

        string[] subtags = tag.Split('-');
        for (int index = 0; index < subtags.Length; index++)
        {
            string subtag = subtags[index];
            subtags[index] = index switch
            {
                0 => subtag.ToLowerInvariant(),
                _ when subtag.Length == 4 && index == 1 && subtag.All(char.IsAsciiLetter) =>
                    char.ToUpperInvariant(subtag[0]) + subtag[1..].ToLowerInvariant(),
                _ when subtag.Length == 2 => subtag.ToUpperInvariant(),
                _ => subtag.ToLowerInvariant(),
            };
        }

        return string.Join('-', subtags);
    }

    [GeneratedRegex(Pattern, RegexOptions.CultureInvariant)]
    private static partial Regex TagPattern();
}
