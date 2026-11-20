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
    /// Tracks of several artists without a common album artist. Always false since an album is identified by one
    /// artist and title; kept for compatibility.
    /// </summary>
    public required bool IsCompilation { get; init; }
}
