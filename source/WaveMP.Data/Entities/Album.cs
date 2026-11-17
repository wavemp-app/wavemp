namespace WaveMP.Data.Entities;

/// <summary>
/// An album, unique by its album artist (or compilation) and title. Created by the scan and removed once it has no
/// tracks. The spelling of the title and the year follow the folder of the album that was written last.
/// </summary>
public class Album
{
    public Guid Id { get; set; }

    public required string Title { get; set; }

    /// <summary>
    /// Upper-case form of <see cref="Title"/> with normalized white space.
    /// </summary>
    public required string NormalizedTitle { get; set; }

    /// <summary>
    /// SHA-256 (hex) of the normalized album artist (or compilation) and title; carries the unique index.
    /// </summary>
    public required string AlbumKey { get; set; }

    /// <summary>
    /// Artist of the album; <c>null</c> for compilations and albums without a known artist.
    /// </summary>
    public Guid? AlbumArtistId { get; set; }

    public Artist? AlbumArtist { get; set; }

    /// <summary>
    /// Tracks of several artists without a common album artist. Clients show their own text for it.
    /// </summary>
    public required bool IsCompilation { get; set; }

    public int? Year { get; set; }

    public required DateTimeOffset FirstSeenAt { get; set; }

    public required DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// The cover rule of the album, set by the admin: whether the album and its tracks show the cover of the album or
    /// the covers of the tracks first. <c>null</c> inherits the global rule of the library settings. A scan never
    /// changes it. Stored as its name.
    /// </summary>
    public CoverPriority? CoverPriority { get; set; }

    public ICollection<Track> Tracks { get; set; } = [];

    public ICollection<Cover> Covers { get; set; } = [];
}
