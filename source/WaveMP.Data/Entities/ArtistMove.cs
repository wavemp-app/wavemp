namespace WaveMP.Data.Entities;

/// <summary>
/// Songs that left one artist for another because the name of their artist or album artist changed (metadata editor,
/// online search, tags changed outside WaveMP, a new version of the album rules). A song counts for an artist as its
/// track artist and as the album artist of its album; it moves from an old artist only when the old artist has neither
/// role on the song any more. One row per pair of artists; the track count adds up over all moves. It is stored
/// together with the changed tracks, so a move survives an interrupted scan. Once <see cref="FromArtistId"/> has no
/// tracks and no albums left, its favorites, its first seen date and a list in the player started from it go to the
/// artists that got its songs and the artist is removed with its moves. While it still has songs, nothing is carried
/// over and the row waits.
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
