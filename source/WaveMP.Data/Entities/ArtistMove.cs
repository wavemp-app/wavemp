namespace WaveMP.Data.Entities;

/// <summary>
/// Songs that left one artist for another because the name of their artist or album artist changed (metadata editor,
/// online search, tags changed outside WaveMP, a renamed or moved folder whose name gives the artist, a new version of
/// the album rules). A song counts for an artist as its track artist and as the album artist of its album; it moves
/// from an old artist only when the old artist has neither role on the song any more. One row per pair of artists; the
/// track count adds up over all moves. It is stored together with the changed tracks (or with the deletion of a
/// vanished folder whose songs were found elsewhere), so a move survives an interrupted scan. Once
/// <see cref="FromArtistId"/> has no tracks and no albums left, its favorites and its first seen date go to every
/// artist that got its songs, and a list in the player started from it, its uploaded and its online picture and its
/// MusicBrainz artist to the main successor (most songs, then the earliest first seen date, then the smallest id; the
/// pictures and the MusicBrainz artist only if it has none); then the artist is removed with its moves. While it still
/// has songs, nothing is carried over and the row waits.
/// </summary>
public class ArtistMove
{
    /// <summary>
    /// The artist that lost the songs.
    /// </summary>
    public required Guid FromArtistId { get; set; }

    public Artist? FromArtist { get; set; }

    /// <summary>
    /// The artist that got the songs.
    /// </summary>
    public required Guid ToArtistId { get; set; }

    public Artist? ToArtist { get; set; }

    /// <summary>
    /// The number of songs moved so far; the artist that got the most songs is the main successor.
    /// </summary>
    public required int TrackCount { get; set; }

    /// <summary>
    /// When songs moved last, for diagnostics.
    /// </summary>
    public required DateTimeOffset MovedAt { get; set; }
}
