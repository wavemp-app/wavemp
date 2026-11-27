using WaveMP.Data.Texts;

namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// The titles and artists of a track by their normalized names (see <see cref="NameKeys"/>), to tell whether a track
/// of LRCLIB is the same song: its title (also without additions, see <see cref="LyricsTitles.Clean"/>) has to match
/// one of the titles, and one of its artists one of the artists, also if one side names several artists ("A &amp; B")
/// and the other only one of them. Without known artists the title alone decides.
/// </summary>
public sealed class LyricsNames
{
    private readonly HashSet<string> _titles;
    private readonly HashSet<string> _artists;

    private LyricsNames(HashSet<string> titles, HashSet<string> artists)
    {
        _titles = titles;
        _artists = artists;
    }

    /// <summary>
    /// Names without titles and artists; nothing matches them.
    /// </summary>
    public static LyricsNames None { get; } = new([], []);

    /// <summary>
    /// Whether at least one artist is known.
    /// </summary>
    public bool HasArtists => _artists.Count > 0;

    /// <summary>
    /// The names of a track.
    /// </summary>
    /// <param name="titles">Its titles, e.g. the title of the tags and the one of the file name.</param>
    /// <param name="artists">Its artists, as named; names of several artists are also split.</param>
    public static LyricsNames Of(IEnumerable<string?> titles, IEnumerable<string?> artists)
    {
        HashSet<string> titleKeys = new(
            titles.OfType<string>().SelectMany(TitleKeys),
            StringComparer.Ordinal);
        HashSet<string> artistKeys = new(
            artists.SelectMany(LyricsTitles.ArtistParts).Select(NameKeys.Of).OfType<string>(),
            StringComparer.Ordinal);
        return new LyricsNames(titleKeys, artistKeys);
    }

    /// <summary>
    /// Whether the track of LRCLIB has one of the titles and one of the artists.
    /// </summary>
    public bool Matches(LrclibRecord record)
    {
        if (record.TrackName is not { } title || !TitleKeys(title).Any(_titles.Contains))
        {
            return false;
        }

        return !HasArtists
            || LyricsTitles.ArtistParts(record.ArtistName).Select(NameKeys.Of).OfType<string>().Any(_artists.Contains);
    }

    private static IEnumerable<string> TitleKeys(string title)
    {
        return new[] { NameKeys.Of(title), NameKeys.Of(LyricsTitles.Clean(title)) }.OfType<string>();
    }
}
