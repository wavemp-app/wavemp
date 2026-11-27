namespace WaveMP.Core.Metadata.Texts;

/// <summary>
/// Reads MusicBrainz ids from tags. Artist tags of a song with several artists (e.g. "A feat. B") name several ids,
/// joined by <c>/</c>, <c>;</c> or a null character; such a tag names no single artist, so it yields none.
/// </summary>
public static class MusicBrainzIds
{
    private static readonly char[] _separators = ['/', ';', ',', '\0', ' '];

    /// <summary>
    /// The id if the text names exactly one; otherwise <c>null</c>.
    /// </summary>
    public static Guid? Single(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string[] parts = text.Split(
            _separators,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 1 && Guid.TryParse(parts[0], out Guid id) && id != Guid.Empty ? id : null;
    }

    /// <summary>
    /// All ids of the text in their order, e.g. the artists of a song with several artists; ids that cannot be read
    /// and repeated ids are left out.
    /// </summary>
    public static IReadOnlyList<Guid> All(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return [.. text.Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => Guid.TryParse(part, out Guid id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .Distinct()];
    }

    /// <summary>
    /// All ids of the texts in their order, in their usual form (lower case, with hyphens).
    /// </summary>
    public static List<string> AllTexts(IEnumerable<string?> texts)
    {
        return [.. texts.SelectMany(All).Distinct().Select(id => id.ToString("D"))];
    }

    /// <summary>
    /// The id in its usual form (lower case, with hyphens) if the text names exactly one; otherwise <c>null</c>.
    /// </summary>
    public static string? SingleText(string? text)
    {
        return Single(text)?.ToString("D");
    }
}
