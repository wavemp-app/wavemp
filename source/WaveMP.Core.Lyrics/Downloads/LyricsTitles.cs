using System.Text.RegularExpressions;
using WaveMP.Core.Metadata.Artists;
using WaveMP.Core.Metadata.Texts;

namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// Prepares titles and artists for the search in LRCLIB: removes additions that name a version of a recording rather
/// than the song (<c>"(Remastered 2011)"</c>, <c>"(Live)"</c>, <c>"- Radio Edit"</c>, <c>"(Official Video)"</c>, every
/// part in square brackets and the featured artists), finds the main artist of a name of several artists and reads
/// artist and title from file names such as <c>"Artist - Title"</c>. The results only serve the lookup; the stored
/// metadata never changes.
/// </summary>
public static partial class LyricsTitles
{
    /// <summary>
    /// What stands between the artist and the title in a file name.
    /// </summary>
    public const string Separator = " - ";

    /// <summary>
    /// The title without its additions, e.g. <c>"Song (Remastered 2011) [HD] feat. Other"</c> becomes
    /// <c>"Song"</c>; the title unchanged if nothing would be left.
    /// </summary>
    public static string Clean(string title)
    {
        string cleaned = title;
        cleaned = SquareBracketPattern().Replace(cleaned, " ");
        cleaned = FeaturingPattern().Replace(cleaned, " ");
        string previous;
        do
        {
            previous = cleaned;
            cleaned = VersionInBracketsPattern().Replace(cleaned, " ");
            cleaned = VersionAfterDashPattern().Replace(cleaned, string.Empty);
        }
        while (!string.Equals(previous, cleaned, StringComparison.Ordinal));

        return MetadataText.Normalize(cleaned) is { } result ? result : title;
    }

    /// <summary>
    /// The first main artist of a name that names several artists, e.g. <c>"A feat. B"</c>, <c>"A &amp; B"</c> or
    /// <c>"A, B"</c> become <c>"A"</c>; the name unchanged if it names one artist.
    /// </summary>
    public static string MainArtist(string artist)
    {
        string main = ArtistNames.Split(artist).FirstOrDefault()?.Name ?? artist;
        return ArtistNames.SuggestSplit(main) is [{ } first, ..] ? first : main;
    }

    /// <summary>
    /// All names of the artists a text names: the text itself, every artist it is split into and their parts at
    /// separators such as <c>"&amp;"</c> or <c>","</c>.
    /// </summary>
    public static IEnumerable<string> ArtistParts(string? artist)
    {
        if (MetadataText.Normalize(artist) is not { } normalized)
        {
            yield break;
        }

        yield return normalized;
        foreach (ArtistName name in ArtistNames.Split(normalized))
        {
            yield return name.Name;
            foreach (string part in ArtistNames.SuggestSplit(name.Name) ?? [])
            {
                yield return part;
            }
        }
    }

    /// <summary>
    /// Artist and title of a file name in the form <c>"Artist - Title"</c> (a leading track number such as
    /// <c>"01 - "</c> is skipped), or <c>null</c> if the name has no such form.
    /// </summary>
    public static (string Artist, string Title)? FromFileName(string baseName)
    {
        string[] parts = [.. baseName.Split(Separator, StringSplitOptions.TrimEntries)];
        int start = 0;
        while (start < parts.Length && parts[start].All(char.IsAsciiDigit))
        {
            start++;
        }

        if (parts.Length - start < 2
            || MetadataText.Normalize(parts[start]) is not { } artist
            || MetadataText.Normalize(string.Join(Separator, parts[(start + 1)..])) is not { } title)
        {
            return null;
        }

        return (artist, title);
    }

    /// <summary>
    /// Every part in square brackets, e.g. <c>"[HD]"</c> or <c>"[Official Video]"</c>.
    /// </summary>
    [GeneratedRegex(@"\[[^\]]*\]", RegexOptions.CultureInvariant)]
    private static partial Regex SquareBracketPattern();

    /// <summary>
    /// Featured artists: <c>"(feat. B)"</c>, <c>"(ft. B)"</c> or <c>"(featuring B)"</c>, or the same without brackets
    /// up to the end of the title or to an opening bracket.
    /// </summary>
    [GeneratedRegex(
        @"\(\s*(?:feat\.?|ft\.?|featuring)\s[^)]*\)|\s(?:feat\.|ft\.|featuring)\s[^(\[]*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FeaturingPattern();

    /// <summary>
    /// A part in round brackets that names a version of the recording, e.g. <c>"(Remastered 2011)"</c>,
    /// <c>"(Live)"</c>, <c>"(Official Music Video)"</c> or <c>"(Radio Edit)"</c>.
    /// </summary>
    [GeneratedRegex(
        @"\(\s*[^()]*?\b(?:remaster(?:ed)?|live|official|video|audio|lyrics?|visuali[sz]er|radio|edit|version"
            + @"|mono|stereo|explicit|clean|hd|hq|4k|single|bonus)\b[^()]*\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionInBracketsPattern();

    /// <summary>
    /// A last part after a dash that names a version of the recording, e.g. <c>" - Radio Edit"</c> or
    /// <c>" - Remastered 2011"</c>.
    /// </summary>
    [GeneratedRegex(
        @"\s+[-–—]\s+[^-–—]*?\b(?:remaster(?:ed)?|live|official|video|audio|lyrics?"
            + @"|visuali[sz]er|radio edit|edit|version|mono|stereo|explicit|single)\b[^-–—]*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VersionAfterDashPattern();
}
