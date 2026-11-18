using System.Globalization;
using System.Text;

namespace WaveMP.Core.Subsonic.Browsing;

/// <summary>
/// The indexes of <c>getIndexes</c> and <c>getArtists</c>: artists grouped by the first letter of their name without
/// a leading article (the ignored articles of the response), letters without accents, everything else under
/// <c>#</c>, which comes last.
/// </summary>
public static class SubsonicIndexes
{
    /// <summary>
    /// The articles that are ignored at the start of names, separated by spaces as the Subsonic API expects.
    /// </summary>
    public const string IgnoredArticles = "The El La Los Las Le Les";

    public const string OtherIndex = "#";

    private static readonly string[] _articles = IgnoredArticles.Split(' ');

    private static readonly StringComparer _nameComparer =
        StringComparer.Create(CultureInfo.InvariantCulture, CompareOptions.IgnoreCase);

    /// <summary>
    /// The name without a leading article, e.g. "Beatles" for "The Beatles".
    /// </summary>
    public static string SortName(string name)
    {
        string trimmed = name.Trim();
        foreach (string article in _articles)
        {
            if (trimmed.Length > article.Length + 1
                && trimmed.StartsWith(article, StringComparison.OrdinalIgnoreCase)
                && char.IsWhiteSpace(trimmed[article.Length]))
            {
                return trimmed[(article.Length + 1)..].TrimStart();
            }
        }

        return trimmed;
    }

    /// <summary>
    /// The index of a name: its first letter in upper case without accents, otherwise <see cref="OtherIndex"/>.
    /// </summary>
    public static string IndexOf(string name)
    {
        string sortName = SortName(name);
        if (sortName.Length == 0)
        {
            return OtherIndex;
        }

        string first = char.IsSurrogate(sortName[0]) ? OtherIndex : sortName[..1];
        string decomposed = first.Normalize(NormalizationForm.FormD);
        char letter = decomposed[0];
        return char.IsLetter(letter)
            ? char.ToUpperInvariant(letter).ToString()
            : OtherIndex;
    }

    /// <summary>
    /// Groups the items by the index of their names: indexes in alphabetical order with <see cref="OtherIndex"/>
    /// last, items of an index by their names without article.
    /// </summary>
    public static IReadOnlyList<(string Index, IReadOnlyList<T> Items)> Group<T>(
        IEnumerable<T> items,
        Func<T, string> nameOf)
    {
        return [.. items
            .GroupBy(item => IndexOf(nameOf(item)), StringComparer.Ordinal)
            .OrderBy(group => group.Key == OtherIndex ? 1 : 0)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (group.Key, SortItems(group, nameOf)))];
    }

    private static IReadOnlyList<T> SortItems<T>(IEnumerable<T> items, Func<T, string> nameOf)
    {
        return [.. items
            .OrderBy(item => SortName(nameOf(item)), _nameComparer)
            .ThenBy(item => nameOf(item), StringComparer.Ordinal)];
    }
}
