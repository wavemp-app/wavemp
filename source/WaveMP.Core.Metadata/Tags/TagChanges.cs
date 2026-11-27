using WaveMP.Core.Metadata.Artists;

namespace WaveMP.Core.Metadata.Tags;

/// <summary>
/// Changes of the tags of an audio file. A field that is <c>null</c> stays as it is; a <see cref="TagChange{T}"/>
/// with the value <c>null</c> removes the field.
/// </summary>
public sealed record TagChanges
{
    /// <summary>
    /// No changes.
    /// </summary>
    public static TagChanges None { get; } = new();

    public TagChange<string?>? Title { get; init; }

    /// <summary>
    /// The artist; replaces all artists of the file.
    /// </summary>
    public TagChange<string?>? Artist { get; init; }

    public TagChange<string?>? Album { get; init; }

    /// <summary>
    /// The album artist; replaces all album artists of the file.
    /// </summary>
    public TagChange<string?>? AlbumArtist { get; init; }

    public TagChange<int?>? Year { get; init; }

    /// <summary>
    /// The number of the track; the number of tracks stays, unless the number is removed.
    /// </summary>
    public TagChange<int?>? TrackNumber { get; init; }

    /// <summary>
    /// The number of the disc; the number of discs stays, unless the number is removed.
    /// </summary>
    public TagChange<int?>? DiscNumber { get; init; }

    /// <summary>
    /// The genre; replaces all genres of the file.
    /// </summary>
    public TagChange<string?>? Genre { get; init; }

    /// <summary>
    /// All album artists; replaces all album artists of the file and wins over <see cref="AlbumArtist"/>. An empty
    /// list removes the field. Puts back the values of a backup.
    /// </summary>
    public TagChange<IReadOnlyList<string>>? AlbumArtists { get; init; }

    /// <summary>
    /// All genres; replaces all genres of the file and wins over <see cref="Genre"/>. An empty list removes the field.
    /// </summary>
    public TagChange<IReadOnlyList<string>>? Genres { get; init; }

    /// <summary>
    /// All artists with their roles; wins over <see cref="Artist"/>. Written as a display text into the main field
    /// (see <see cref="ArtistNames.DisplayText"/>) and as separate values into the field ARTISTS where the format
    /// has one. An empty list removes the artists.
    /// </summary>
    public TagChange<IReadOnlyList<ArtistName>>? ArtistCredits { get; init; }

    /// <summary>
    /// All album artists; wins over <see cref="AlbumArtist"/> and <see cref="AlbumArtists"/>. Written as a display
    /// text into the main field (the names joined by <see cref="ArtistNames.Separator"/>) and as separate values into
    /// the field ALBUMARTISTS where the format has one. An empty list removes the album artists.
    /// </summary>
    public TagChange<IReadOnlyList<string>>? AlbumArtistCredits { get; init; }

    /// <summary>
    /// The number of tracks; written after <see cref="TrackNumber"/>.
    /// </summary>
    public TagChange<int?>? TrackCount { get; init; }

    /// <summary>
    /// The number of discs; written after <see cref="DiscNumber"/>.
    /// </summary>
    public TagChange<int?>? DiscCount { get; init; }

    /// <summary>
    /// Whether nothing changes.
    /// </summary>
    public bool IsEmpty => Title is null
        && Artist is null
        && Album is null
        && AlbumArtist is null
        && Year is null
        && TrackNumber is null
        && DiscNumber is null
        && Genre is null
        && AlbumArtists is null
        && Genres is null
        && TrackCount is null
        && DiscCount is null
        && ArtistCredits is null
        && AlbumArtistCredits is null;
}
