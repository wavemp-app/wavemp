using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// A square section of a picture as fractions of its size, e.g. from a cropper over the preview: the left edge at x
/// times the width, the top edge at y times the height, the side size times the shorter side. A section that would
/// leave the picture is moved back into it.
/// </summary>
public sealed class ArtistPictureCropDto
{
    /// <summary>
    /// Left edge, 0 to 1 of the width.
    /// </summary>
    [Required]
    [Range(0d, 1d)]
    public required double? X { get; init; }

    /// <summary>
    /// Top edge, 0 to 1 of the height.
    /// </summary>
    [Required]
    [Range(0d, 1d)]
    public required double? Y { get; init; }

    /// <summary>
    /// Side, more than 0 and at most 1 of the shorter side.
    /// </summary>
    [Required]
    [Range(0.0001d, 1d)]
    public required double? Size { get; init; }
}
