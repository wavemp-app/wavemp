using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Changes an own album. Missing or null values stay unchanged; texts are trimmed. Changing an album moves no file.
/// </summary>
public sealed class UpdateOwnAlbumDto
{
    /// <summary>
    /// New title, 1 to 512 characters.
    /// </summary>
    [StringLength(512, MinimumLength = 1)]
    [RegularExpression(@".*\S.*")]
    public string? Title { get; init; }

    /// <summary>
    /// New album artist, 1 to 512 characters.
    /// </summary>
    [StringLength(512, MinimumLength = 1)]
    [RegularExpression(@".*\S.*")]
    public string? AlbumArtist { get; init; }

    /// <summary>
    /// New year (1 to 9999); 0 removes it, so each song keeps its own year.
    /// </summary>
    [Range(0, 9999)]
    public int? Year { get; init; }

    /// <summary>
    /// New genre, at most 128 characters; empty removes it, so each song keeps its own genre.
    /// </summary>
    [StringLength(128)]
    public string? Genre { get; init; }
}
