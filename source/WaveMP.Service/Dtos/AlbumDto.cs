namespace WaveMP.Service.Dtos;

/// <summary>
/// An album of the library.
/// </summary>
public sealed class AlbumDto
{
    public required Guid Id { get; init; }

    public required string Title { get; init; }

    /// <summary>
    /// Artist of the album: the album artist of its tracks, otherwise their artist; null for albums without a known
    /// artist.
    /// </summary>
    public ArtistReferenceDto? Artist { get; init; }

    /// <summary>
    /// Whether the album is a compilation: at least one of its tracks is marked as part of a compilation in its tags
    /// (ID3 TCMP, MP4 cpil, Vorbis comment COMPILATION) or its artist is "Various Artists" (letter case and spaces at
    /// the start and end do not matter). Set by the scan.
    /// </summary>
    public required bool IsCompilation { get; init; }

    public int? Year { get; init; }

    /// <summary>
    /// The first genre of its tracks in the order of disc and track number.
    /// </summary>
    public string? Genre { get; init; }

    public required int TrackCount { get; init; }

    /// <summary>
    /// Sum of the durations of the tracks in milliseconds.
    /// </summary>
    public required long DurationMilliseconds { get; init; }

    /// <summary>
    /// Whether the album or one of its tracks has a cover (GetAlbumCover).
    /// </summary>
    public required bool HasCover { get; init; }

    /// <summary>
    /// When the album was found for the first time.
    /// </summary>
    public required DateTimeOffset FirstSeenAt { get; init; }

    /// <summary>
    /// Whether the current profile marked the album as favorite.
    /// </summary>
    public required bool IsFavorite { get; init; }
}
