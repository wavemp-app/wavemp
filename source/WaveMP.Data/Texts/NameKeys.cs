using System.Globalization;
using System.Text;

namespace WaveMP.Data.Texts;

/// <summary>
/// The normalized names that artists, albums and genres are found by: lower case, diacritics removed (the text is
/// decomposed and its combining diacritical marks dropped), and every character that is neither a letter nor a digit
/// removed, so "K.Flay" becomes <c>kflay</c>, "K/DA" <c>kda</c> and "Beyoncé" <c>beyonce</c>. Letters, digits and the
/// marks that belong to them (e.g. vowel signs) of every script are kept. A name without any letter or digit (e.g.
/// "÷" or "!!!") keeps its characters in lower case with its white space joined, so such names stay apart. The key
/// is only used to find entries and to sort them, never shown; references between entries are always stored by id.
/// </summary>
public static class NameKeys
{
    /// <summary>
    /// Longest key; longer keys are cut, like the names they come from.
    /// </summary>
    public const int MaxLength = 512;

    /// <summary>
    /// The key of a name; <c>null</c> for <c>null</c> and names without content.
    /// </summary>
    public static string? Of(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        StringBuilder builder = new(name.Length);
        foreach (Rune rune in name.Normalize(NormalizationForm.FormD).EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune) || IsMarkOfAScript(rune))
            {
                builder.Append(Rune.ToLowerInvariant(rune).ToString());
            }
        }

        string key = builder.Length > 0 ? builder.ToString() : Symbols(name);
        return key.Length == 0 ? null : Cut(key.Normalize(NormalizationForm.FormC));
    }

    /// <summary>
    /// Marks that belong to the letters of their script, such as the vowel signs of Devanagari or the voicing marks of
    /// Kana, unlike the diacritics of the combining diacritical marks blocks (accents, umlauts, cedillas and the like),
    /// which are removed.
    /// </summary>
    private static bool IsMarkOfAScript(Rune rune)
    {
        UnicodeCategory category = Rune.GetUnicodeCategory(rune);
        if (category is not (UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark))
        {
            return false;
        }

        int value = rune.Value;
        bool diacritic = value is (>= 0x0300 and <= 0x036F)
            or (>= 0x1AB0 and <= 0x1AFF)
            or (>= 0x1DC0 and <= 0x1DFF)
            or (>= 0x20D0 and <= 0x20FF)
            or (>= 0xFE20 and <= 0xFE2F);
        return !diacritic;
    }

    /// <summary>
    /// A name without letters and digits: its characters in lower case, white space and control characters joined to
    /// single spaces and removed at the start and end.
    /// </summary>
    private static string Symbols(string name)
    {
        StringBuilder builder = new(name.Length);
        bool space = false;
        foreach (Rune rune in name.Normalize(NormalizationForm.FormC).EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune) || Rune.IsControl(rune))
            {
                space = builder.Length > 0;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(Rune.ToLowerInvariant(rune).ToString());
        }

        return builder.ToString();
    }

    private static string Cut(string key)
    {
        if (key.Length <= MaxLength)
        {
            return key;
        }

        int length = char.IsHighSurrogate(key[MaxLength - 1]) ? MaxLength - 1 : MaxLength;
        return key[..length];
    }

    /// <summary>
    /// Whether two names have the same key.
    /// </summary>
    public static bool AreEqual(string? left, string? right)
    {
        return string.Equals(Of(left), Of(right), StringComparison.Ordinal);
    }
}
