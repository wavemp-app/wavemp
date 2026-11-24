using System.Text;

namespace WaveMP.Core.Library.Groups;

/// <summary>
/// The letter a text is filed under in an index A to Z: the first code point of the text in Unicode NFD (so accents
/// do not count), an ASCII letter in upper case; everything else (digits, punctuation, other scripts, letters without
/// a decomposition to A to Z such as the sharp s, an empty text) is filed under <see cref="Other"/>. Independent of
/// the culture; the web app files texts the same way.
/// </summary>
public static class IndexLetters
{
    /// <summary>
    /// The entry for digits and special characters.
    /// </summary>
    public const string Other = "#";

    public static string Of(string? text)
    {
        // A character outside the basic plane never decomposes to A to Z; a lone surrogate is no character at all.
        if (string.IsNullOrEmpty(text) || char.IsSurrogate(text[0]))
        {
            return Other;
        }

        char first = text[..1].Normalize(NormalizationForm.FormD)[0];
        return char.IsAsciiLetter(first) ? char.ToUpperInvariant(first).ToString() : Other;
    }
}
