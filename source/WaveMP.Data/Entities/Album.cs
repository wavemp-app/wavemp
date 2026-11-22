namespace WaveMP.Data.Entities;

/// <summary>
/// An album, unique by its artist and title as the metadata names them: the album artist of its tracks, otherwise
/// their artist. Albums without artist (tracks without any artist, and tracks with the compilation flag but without
/// album artist tag) are unique by their title and album folder in the media source instead. Created by the scan and
/// removed once it has no tracks. The spelling of the title and the year follow the folder of the album that was
/// written last. When its tracks moved to other albums (see <see cref="AlbumMove"/>), its favorites, cover rule, online
/// cover and <see cref="FirstSeenAt"/> go to those albums before it is removed
/// (<see cref="Repositories.ILibraryScanRepository.DeleteOrphansAsync"/>).
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
    /// SHA-256 (hex) of the normalized artist (the album folder for albums without artist) and title; carries the
    /// unique index.
    /// </summary>
    public required string AlbumKey { get; set; }

    /// <summary>
    /// Artist of the album: the album artist of its tracks, otherwise their artist; <c>null</c> for albums without a
    /// known artist and for compilations formed by the compilation flag without an album artist tag.
    /// </summary>
    public Guid? AlbumArtistId { get; set; }

    public Artist? AlbumArtist { get; set; }

    /// <summary>
    /// Whether the album is a compilation: at least one of its tracks is marked as part of a compilation in the tags of
    /// the file it takes its metadata from, or its artist is "Various Artists". The scan sets it over all tracks of the
    /// album once every folder is written.
    /// </summary>
    public required bool IsCompilation { get; set; }

    public int? Year { get; set; }

    /// <summary>
    /// When the scan first found the album; an album that got the tracks of removed albums keeps the earliest date of
    /// them all.
    /// </summary>
    public required DateTimeOffset FirstSeenAt { get; set; }

    public required DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// The cover rule of the album, set by the admin: whether the album and its tracks show the cover of the album or
    /// the covers of the tracks first. <c>null</c> inherits the global rule of the library settings. A scan never
    /// changes it, except that an album without a rule of its own takes the rule of an album whose tracks it got once
    /// that album is removed. Stored as its name.
    /// </summary>
    public CoverPriority? CoverPriority { get; set; }

    public ICollection<Track> Tracks { get; set; } = [];

    public ICollection<Cover> Covers { get; set; } = [];
}
