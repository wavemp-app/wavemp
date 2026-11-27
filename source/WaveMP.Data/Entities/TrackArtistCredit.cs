namespace WaveMP.Data.Entities;

/// <summary>
/// An artist of a song, by id, with its place in the order of the artists of the song and its role. A song has each
/// artist at most once. The first artist (position 0) is also stored as <see cref="Track.ArtistId"/>, which sorts and
/// groups the songs and serves the fields that name a single artist. The song appears on the pages of all its artists
/// and counts for all of them in statistics, recaps and the radio.
/// </summary>
public class TrackArtistCredit
{
    public required Guid TrackId { get; set; }

    public Track? Track { get; set; }

    public required Guid ArtistId { get; set; }

    public Artist? Artist { get; set; }

    /// <summary>
    /// Place of the artist in the order of the artists of the song, from 0.
    /// </summary>
    public required int Position { get; set; }

    /// <summary>
    /// Main or featured artist. Stored as its name.
    /// </summary>
    public required ArtistRole Role { get; set; }
}
