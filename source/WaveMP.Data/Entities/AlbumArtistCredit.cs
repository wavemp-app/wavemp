namespace WaveMP.Data.Entities;

/// <summary>
/// An album artist of an album, by id, with its place in the order of the album artists. An album has each artist at
/// most once. The first one (position 0) is also stored as <see cref="Album.AlbumArtistId"/>, which sorts and groups
/// the albums and serves the fields that name a single artist. The album appears on the pages of all its album
/// artists.
/// </summary>
public class AlbumArtistCredit
{
    public required Guid AlbumId { get; set; }

    public Album? Album { get; set; }

    public required Guid ArtistId { get; set; }

    public Artist? Artist { get; set; }

    /// <summary>
    /// Place of the artist in the order of the album artists, from 0.
    /// </summary>
    public required int Position { get; set; }
}
