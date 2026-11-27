using System.Text;

namespace WaveMP.Data.Texts;

/// <summary>
/// The keys the library search finds names by, on top of their normalized names (see <see cref="NameKeys"/>): the
/// symbol variant reads the symbols that stand for letters as those letters, so "P!nk" becomes <c>pink</c>, "Ke$ha"
/// <c>kesha</c> and "Simon &amp; Garfunkel" <c>simonandgarfunkel</c>. Search texts get the same treatment, so the
/// replacements work in both directions: "pink" finds "P!nk" and "p!nk" finds "Pink". The keys only serve the
/// search; the scan never finds or joins entries by them.
/// </summary>
public static class SearchKeys
{
    /// <summary>
    /// Longest key: the normalized name, a space and the symbol variant.
    /// </summary>
    public const int MaxLength = (NameKeys.MaxLength * 2) + 1;

    /// <summary>
    /// The word that <c>&amp;</c> and <c>+</c> stand for.
    /// </summary>
    private const string And = "and";

    /// <summary>
    /// The stored search key of a name: its normalized name, followed by a space and its symbol variant if that
    /// differs (e.g. <c>pnk pink</c> for "P!nk"), so a <c>LIKE</c> on one column finds both forms; neither part
    /// contains a space. <c>null</c> for names without content.
    /// </summary>
    public static string? Of(string? name)
    {
        string? key = NameKeys.Of(name);
        if (key is null)
        {
            return null;
        }

        string? variant = Variant(name);
        return variant is null || string.Equals(variant, key, StringComparison.Ordinal) ? key : $"{key} {variant}";
    }

    /// <summary>
    /// The symbol variant of a name or search text: the normalized name (see <see cref="NameKeys"/>) after
    /// <c>&amp;</c> and <c>+</c> became "and" and, in words with at least one letter, <c>!</c> before a letter became
    /// <c>i</c>, <c>$</c> <c>s</c>, <c>@</c> <c>a</c>, <c>0</c> <c>o</c> and <c>3</c> <c>e</c>. Words without letters
    /// keep their digits, so "3 Doors Down", "50 Cent" or a year stay as they are. <c>null</c> for texts without
    /// content.
    /// </summary>
    public static string? Variant(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? null : NameKeys.Of(ReplaceSymbols(text));
    }

    /// <summary>
    /// The text with its symbols replaced as described in <see cref="Variant"/>, words still apart.
    /// </summary>
    internal static string ReplaceSymbols(string text)
    {
        StringBuilder builder = new(text.Length + 8);
        foreach (string word in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            bool hasLetter = word.Any(char.IsLetter);
            for (int index = 0; index < word.Length; index++)
            {
                char character = word[index];
                switch (character)
                {
                    case '&' or '+':
                        builder.Append(' ').Append(And).Append(' ');
                        break;
                    case '!' when hasLetter && index + 1 < word.Length && char.IsLetter(word[index + 1]):
                        builder.Append('i');
                        break;
                    case '$' when hasLetter:
                        builder.Append('s');
                        break;
                    case '@' when hasLetter:
                        builder.Append('a');
                        break;
                    case '0' when hasLetter:
                        builder.Append('o');
                        break;
                    case '3' when hasLetter:
                        builder.Append('e');
                        break;
                    default:
                        builder.Append(character);
                        break;
                }
            }
        }

        return builder.ToString();
    }
}
