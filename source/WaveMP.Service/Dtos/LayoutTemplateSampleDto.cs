using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// An example song for the preview of a layout template, e.g. while the library is empty. Texts are at most 512
/// characters (the genre 128), numbers 1 to 9999.
/// </summary>
public sealed class LayoutTemplateSampleDto
{
    [Required]
    [StringLength(512)]
    public required string Title { get; init; }

    [StringLength(512)]
    public string? Artist { get; init; }

    [StringLength(512)]
    public string? AlbumArtist { get; init; }

    [StringLength(512)]
    public string? Album { get; init; }

    [Range(1, 9999)]
    public int? Year { get; init; }

    [Range(1, 9999)]
    public int? TrackNumber { get; init; }

    [Range(1, 9999)]
    public int? DiscNumber { get; init; }

    [StringLength(128)]
    public string? Genre { get; init; }

    /// <summary>
    /// Whether the album is a compilation: without album artist, <c>{AlbumArtist}</c> becomes "Various Artists".
    /// </summary>
    public bool IsCompilation { get; init; }

    /// <summary>
    /// Extension of the file without dot, e.g. <c>flac</c>; letters and digits only, otherwise ignored.
    /// </summary>
    [StringLength(10)]
    public string? Extension { get; init; }
}
