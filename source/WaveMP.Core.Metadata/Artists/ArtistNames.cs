using System.Text.RegularExpressions;
using WaveMP.Core.Metadata.Texts;
using WaveMP.Data.Entities;
using WaveMP.Data.Texts;

namespace WaveMP.Core.Metadata.Artists;

/// <summary>
/// Turns the artist tags of a file into a list of artists and back. Real multi-values (several values of a field, or
/// the fields <c>ARTISTS</c> and <c>ALBUMARTISTS</c>) are taken over as they are. A single text is split only at
/// unambiguous separators: "feat.", "ft." and "featuring" (the artists after them are featured artists) and ";".
/// Commas, "&amp;", "and", "x" and "/" are never split automatically, since names such as "Earth, Wind &amp; Fire",
/// "Simon &amp; Garfunkel" or "AC/DC" would break; <see cref="SuggestSplit(string?)"/> offers a split at them for the admin to
/// confirm instead.
/// </summary>
public static partial class ArtistNames
{
    /// <summary>
    /// Separator of several main artists (and of several featured ones) in a single text and in the display text the
    /// metadata editor writes.
    /// </summary>
    public const string Separator = "; ";

    /// <summary>
    /// What stands before the featured artists in the display text the metadata editor writes.
    /// </summary>
    public const string FeaturingSeparator = " feat. ";

    /// <summary>
    /// The artists of a single text, e.g. <c>"Bob Sinclar feat. K.Flay"</c> becomes the main artist "Bob Sinclar"
    /// and the featured artist "K.Flay". Names that are equal by their normalized names (see <see cref="NameKeys"/>)
    /// appear once, as main artist if they are one. Empty for <c>null</c> and texts without a name.
    /// </summary>
    public static IReadOnlyList<ArtistName> Split(string? text)
    {
        if (MetadataText.Normalize(text) is not { } normalized)
        {
            return [];
        }

        string main = normalized;
        string? featured = null;
        Match match = FeaturingPattern().Match(normalized);
        if (match.Success && match.Index > 0)
        {
            main = normalized[..match.Index];
            featured = normalized[(match.Index + match.Length)..];
            if (match.Value.IndexOfAny(['(', '[']) >= 0)
            {
                int end = featured.IndexOfAny([')', ']']);
                featured = end >= 0 ? featured[..end] : featured;
            }
        }

        List<ArtistName> artists = [.. Parts(main).Select(name => new ArtistName(name))];
        artists.AddRange(Parts(featured).Select(name => new ArtistName(name, ArtistRole.Featured)));
        return Distinct(artists.Count > 0 ? artists : [new ArtistName(normalized)]);
    }

    /// <summary>
    /// The artists of a field with real multi-values, in their order. The roles come from the display text of the
    /// main field: a value that <see cref="Split"/> finds after "feat." there is a featured artist. The MusicBrainz
    /// artists are given to the values by their order if the tags name as many as there are values.
    /// </summary>
    /// <param name="values">The values of the field.</param>
    /// <param name="displayText">The text of the main field, e.g. <c>"Bob Sinclar feat. K.Flay"</c>.</param>
    /// <param name="musicBrainzArtistIds">The MusicBrainz artists of the field in their order.</param>
    public static IReadOnlyList<ArtistName> FromValues(
        IReadOnlyList<string> values,
        string? displayText,
        IReadOnlyList<Guid> musicBrainzArtistIds)
    {
        string[] names = [.. values.Select(MetadataText.Normalize).OfType<string>()];
        HashSet<string> featured = new(
            Split(displayText)
                .Where(artist => artist.Role == ArtistRole.Featured)
                .Select(artist => NameKeys.Of(artist.Name))
                .OfType<string>(),
            StringComparer.Ordinal);
        bool hasIds = musicBrainzArtistIds.Count == names.Length;
        return Distinct([.. names.Select((name, index) => new ArtistName(
            name,
            NameKeys.Of(name) is { } key && featured.Contains(key) ? ArtistRole.Featured : ArtistRole.Main,
            hasIds ? musicBrainzArtistIds[index] : null))]);
    }

    /// <summary>
    /// The artists of a song or album: the real multi-values if there are any, otherwise the single text split at
    /// the unambiguous separators. A single artist gets the MusicBrainz artist of the tags, if they name exactly one.
    /// </summary>
    /// <param name="values">The values of a field with real multi-values; empty if the tags hold one text only.</param>
    /// <param name="text">The text of the main field.</param>
    /// <param name="musicBrainzArtistIds">The MusicBrainz artists of the field in their order.</param>
    public static IReadOnlyList<ArtistName> Resolve(
        IReadOnlyList<string> values,
        string? text,
        IReadOnlyList<Guid> musicBrainzArtistIds)
    {
        IReadOnlyList<ArtistName> artists = values.Any(value => MetadataText.Normalize(value) is not null)
            ? FromValues(values, text, musicBrainzArtistIds)
            : Split(text);
        return artists is [{ MusicBrainzArtistId: null } single] && musicBrainzArtistIds is [Guid id]
            ? [single with { MusicBrainzArtistId = id }]
            : artists;
    }

    /// <summary>
    /// The text the metadata editor writes into the main artist field for older players: the main artists joined by
    /// <see cref="Separator"/>, then <see cref="FeaturingSeparator"/> and the featured artists, e.g.
    /// <c>"Bob Sinclar feat. K.Flay"</c>. <see cref="Split"/> reads it back into the same artists. <c>null</c>
    /// without artists.
    /// </summary>
    public static string? DisplayText(IReadOnlyList<ArtistName> artists)
    {
        string main = string.Join(
            Separator,
            artists.Where(artist => artist.Role == ArtistRole.Main).Select(artist => artist.Name));
        string featured = string.Join(
            Separator,
            artists.Where(artist => artist.Role == ArtistRole.Featured).Select(artist => artist.Name));
        if (main.Length == 0)
        {
            return featured.Length == 0 ? null : featured;
        }

        return featured.Length == 0 ? main : main + FeaturingSeparator + featured;
    }

    /// <summary>
    /// A split of a name at the separators that are never split automatically (comma, slash, and "&amp;", "and",
    /// "x" and "+" between spaces), for the admin to confirm, e.g. <c>"Bob Sinclar &amp; K.Flay"</c> becomes
    /// "Bob Sinclar" and "K.Flay"; <c>null</c> if the name has none of them.
    /// </summary>
    public static IReadOnlyList<string>? SuggestSplit(string? name)
    {
        if (MetadataText.Normalize(name) is not { } normalized)
        {
            return null;
        }

        string[] parts = [.. AmbiguousPattern().Split(normalized)
            .Select(MetadataText.Normalize)
            .OfType<string>()
            .DistinctBy(part => NameKeys.Of(part) ?? part, StringComparer.Ordinal)];
        return parts.Length > 1 ? parts : null;
    }

    /// <summary>
    /// The artists of a song with every name that <see cref="SuggestSplit(string?)"/> would split replaced by its
    /// parts (with the role of the name), for the admin to confirm; <c>null</c> if no name would be split.
    /// </summary>
    public static IReadOnlyList<ArtistName>? SuggestSplit(IReadOnlyList<ArtistName> artists)
    {
        bool split = false;
        List<ArtistName> result = [];
        foreach (ArtistName artist in artists)
        {
            if (SuggestSplit(artist.Name) is { } parts)
            {
                split = true;
                result.AddRange(parts.Select(part => new ArtistName(part, artist.Role)));
            }
            else
            {
                result.Add(artist);
            }
        }

        return split ? Distinct(result) : null;
    }

    private static IEnumerable<string> Parts(string? text)
    {
        return text is null
            ? []
            : text.Split(';').Select(MetadataText.Normalize).OfType<string>();
    }

    private static List<ArtistName> Distinct(IReadOnlyList<ArtistName> artists)
    {
        List<ArtistName> result = [];
        Dictionary<string, int> positions = new(StringComparer.Ordinal);
        foreach (ArtistName artist in artists)
        {
            string key = NameKeys.Of(artist.Name) ?? artist.Name;
            if (!positions.TryGetValue(key, out int position))
            {
                positions[key] = result.Count;
                result.Add(artist);
            }
            else if (artist.Role == ArtistRole.Main && result[position].Role == ArtistRole.Featured)
            {
                result[position] = result[position] with { Role = ArtistRole.Main };
            }
        }

        return result;
    }

    /// <summary>
    /// "feat.", "ft." or "featuring" after a space or an opening bracket.
    /// </summary>
    [GeneratedRegex(
        @"(?:\s+[(\[]?|[(\[])\s*(?:feat\.|ft\.|featuring(?=\s))\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FeaturingPattern();

    /// <summary>
    /// A comma, a slash, or "&amp;", "and", "x" and "+" between spaces.
    /// </summary>
    [GeneratedRegex(
        @"\s*,\s*|\s*/\s*|\s+(?:&|and|x|\+)\s+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AmbiguousPattern();
}
