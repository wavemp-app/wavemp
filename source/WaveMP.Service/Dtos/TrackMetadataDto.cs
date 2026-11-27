namespace WaveMP.Service.Dtos;

/// <summary>
/// The metadata of a track as the metadata editor shows it: the values of the tags of its audio file; values the
/// tags do not contain come from the library (e.g. derived from the folder and file name).
/// </summary>
public sealed class TrackMetadataDto
{
    /// <summary>
    /// Id of the track.
    /// </summary>
    public required Guid TrackId { get; init; }

    /// <summary>
    /// Whether the editor can write the tags.
    /// </summary>
    public required TrackMetadataStatusDto Status { get; init; }

    /// <summary>
    /// Path of the audio file the editor writes, relative to the media folder; null without audio file.
    /// </summary>
    public string? AudioFilePath { get; init; }

    public required string Title { get; init; }

    public string? Artist { get; init; }

    public string? Album { get; init; }

    public string? AlbumArtist { get; init; }

    /// <summary>
    /// All artists of the track in their order, each with its role: the separate values of the tags, otherwise the
    /// artist field split at "feat.", "ft.", "featuring" and ";".
    /// </summary>
    public required IReadOnlyList<ArtistNameDto> Artists { get; init; }

    /// <summary>
    /// All album artists in their order, read like Artists.
    /// </summary>
    public required IReadOnlyList<string> AlbumArtists { get; init; }

    /// <summary>
    /// Whether the tags hold the artists as separate values (ARTISTS, several values of the artist field), so the
    /// list of artists is confirmed and not split from a single text.
    /// </summary>
    public required bool HasArtistValues { get; init; }

    public int? Year { get; init; }

    /// <summary>
    /// Number of the track on its disc.
    /// </summary>
    public int? TrackNumber { get; init; }

    public int? DiscNumber { get; init; }

    public string? Genre { get; init; }
}
