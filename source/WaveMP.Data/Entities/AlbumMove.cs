namespace WaveMP.Data.Entities;

/// <summary>
/// Songs that left one album for another because the identity of their album changed (album name or album artist
/// edited, tags changed outside WaveMP, a new version of the album rules). One row per pair of albums; the track count
/// adds up over all moves. It is stored together with the changed tracks, so a move survives an interrupted scan.
/// Once <see cref="FromAlbumId"/> has no tracks left, its favorites, cover rule, online cover and first seen date go to
/// the albums that got its songs and the album is removed with its moves. While it still has tracks, nothing is carried
/// over and the row waits.
/// </summary>
public class AlbumMove
{
    /// <summary>
    /// The album that lost the songs.
    /// </summary>
    public required Guid FromAlbumId { get; set; }

    public Album? FromAlbum { get; set; }

    /// <summary>
    /// The album that got the songs.
    /// </summary>
    public required Guid ToAlbumId { get; set; }

    public Album? ToAlbum { get; set; }

    /// <summary>
    /// The number of songs moved so far; the album that got the most songs is the main successor.
    /// </summary>
    public required int TrackCount { get; set; }

    /// <summary>
    /// When songs moved last, for diagnostics.
    /// </summary>
    public required DateTimeOffset MovedAt { get; set; }
}
