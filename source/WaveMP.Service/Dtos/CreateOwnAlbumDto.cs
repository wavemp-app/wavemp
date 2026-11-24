using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Creates an own album without songs. Texts are trimmed.
/// </summary>
public sealed class CreateOwnAlbumDto
{
    /// <summary>
    /// Title of the album, 1 to 512 characters.
    /// </summary>
    [Required]
    [StringLength(512)]
    public required string Title { get; init; }

    /// <summary>
    /// Album artist, 1 to 512 characters.
    /// </summary>
    [Required]
    [StringLength(512)]
    public required string AlbumArtist { get; init; }

    /// <summary>
    /// Year written into the songs (1 to 9999); missing keeps the year of each song.
    /// </summary>
    [Range(1, 9999)]
    public int? Year { get; init; }

    /// <summary>
    /// Genre written into the songs, at most 128 characters; missing or empty keeps the genre of each song.
    /// </summary>
    [StringLength(128)]
    public string? Genre { get; init; }
}
