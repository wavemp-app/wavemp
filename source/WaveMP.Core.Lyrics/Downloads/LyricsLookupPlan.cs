using WaveMP.Data.Projections;
using WaveMP.Data.Texts;

namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// What the lookup of a track asks LRCLIB for: the exact lookup with title, artist, album and duration, then the
/// search for title and artist, then for the title without additions and the main artist (see
/// <see cref="LyricsTitles"/>), and finally the free-text search for both. A track without artist takes artist and
/// title from its file name (<c>"Artist - Title"</c>), for the lookup only.
/// </summary>
public sealed record LyricsLookupPlan
{
    /// <summary>
    /// The title to look up.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// The artist to look up, or <c>null</c> if unknown.
    /// </summary>
    public required string? Artist { get; init; }

    public string? Album { get; init; }

    public TimeSpan? Duration { get; init; }

    /// <summary>
    /// The names a found track has to match for the free-text search and for lyrics of another version.
    /// </summary>
    public LyricsNames Names { get; init; } = LyricsNames.None;

    /// <summary>
    /// The title without additions such as <c>"(Remastered)"</c>.
    /// </summary>
    public string CleanTitle => LyricsTitles.Clean(Title);

    /// <summary>
    /// The first main artist, or <c>null</c> without artist.
    /// </summary>
    public string? MainArtist => Artist is null ? null : LyricsTitles.MainArtist(Artist);

    /// <summary>
    /// The query of the exact lookup and of the first search.
    /// </summary>
    public LrclibQuery Query => new(Title, Artist, Album, Duration);

    /// <summary>
    /// The queries of the search: title and artist, then the title without additions and the main artist if they
    /// differ.
    /// </summary>
    public IReadOnlyList<LrclibQuery> SearchQueries
    {
        get
        {
            LrclibQuery first = Query;
            LrclibQuery cleaned = first with { TrackName = CleanTitle, ArtistName = MainArtist };
            return NameKeys.AreEqual(first.TrackName, cleaned.TrackName)
                && NameKeys.AreEqual(first.ArtistName, cleaned.ArtistName)
                    ? [first]
                    : [first, cleaned];
        }
    }

    /// <summary>
    /// The text of the free-text search: the main artist and the title without additions.
    /// </summary>
    public string Text => MainArtist is { } artist ? $"{artist} {CleanTitle}" : CleanTitle;

    /// <summary>
    /// The plan of a track; without artist, artist and title come from the file name if it has the form
    /// <c>"Artist - Title"</c>.
    /// </summary>
    public static LyricsLookupPlan Of(LyricsTrackRow track)
    {
        string title = track.Title;
        string? artist = track.ArtistName;
        if (artist is null && LyricsTitles.FromFileName(track.BaseName) is { } fromFile)
        {
            artist = fromFile.Artist;

            // A title that only repeats the file name was taken from it.
            if (NameKeys.AreEqual(title, track.BaseName))
            {
                title = fromFile.Title;
            }
        }

        string?[] artists = [.. track.Artists.Select(credit => credit.Name), artist];
        return new LyricsLookupPlan
        {
            Title = title,
            Artist = artist,
            Album = track.AlbumTitle,
            Duration = track.Duration,
            Names = LyricsNames.Of([title, track.Title], artists),
        };
    }

    /// <summary>
    /// The plan of a search by an admin with title and artist of their own.
    /// </summary>
    public static LyricsLookupPlan Of(string title, string? artist, string? album, TimeSpan? duration)
    {
        return new LyricsLookupPlan
        {
            Title = title,
            Artist = artist,
            Album = album,
            Duration = duration,
            Names = LyricsNames.Of([title], [artist]),
        };
    }
}
