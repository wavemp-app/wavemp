namespace WaveMP.Service.Dtos;

/// <summary>
/// The album of a track.
/// </summary>
public sealed class AlbumReferenceDto
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
}
