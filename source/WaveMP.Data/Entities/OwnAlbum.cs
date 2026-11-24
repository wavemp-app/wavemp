namespace WaveMP.Data.Entities;

/// <summary>
/// An album the admin puts together from songs of the library. It is only a definition until the admin applies it:
/// then its values are written into the tags of the audio files and the files are moved by the layout template.
/// Deleting it removes the definition only; files and tags stay.
/// </summary>
public class OwnAlbum
{
    public Guid Id { get; set; }

    public required string Title { get; set; }

    /// <summary>
    /// Upper-case form of <see cref="Title"/> for sorting without regard to case.
    /// </summary>
    public required string NormalizedTitle { get; set; }

    public required string AlbumArtist { get; set; }

    /// <summary>
    /// Year written into the songs; <c>null</c> keeps the year of each song.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Genre written into the songs; <c>null</c> keeps the genre of each song.
    /// </summary>
    public string? Genre { get; set; }

    /// <summary>
    /// Name of the uploaded cover in the folder of the own album covers in the data folder, e.g. <c>{id}.jpg</c>;
    /// <c>null</c> without cover.
    /// </summary>
    public string? CoverFileName { get; set; }

    /// <summary>
    /// Content type of the cover, e.g. <c>image/jpeg</c>.
    /// </summary>
    public string? CoverContentType { get; set; }

    /// <summary>
    /// When the cover was uploaded; also the version of the cover.
    /// </summary>
    public DateTimeOffset? CoverUpdatedAt { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public User? CreatedBy { get; set; }

    public required DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// When the values, the cover or the songs changed for the last time.
    /// </summary>
    public required DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// When the album was applied to the files for the last time; <c>null</c> while it was never applied.
    /// </summary>
    public DateTimeOffset? AppliedAt { get; set; }

    public ICollection<OwnAlbumTrack> Tracks { get; set; } = [];
}
