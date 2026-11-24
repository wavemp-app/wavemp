using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Asks where a layout template would put songs. Without <see cref="TrackIds"/> and <see cref="Samples"/> the preview
/// shows the first 3 songs of the library (by album artist, album, disc and track number).
/// </summary>
public sealed class PreviewLayoutTemplateDto
{
    /// <summary>
    /// The layout template to try (see <see cref="OrganizingSettingsDto.LayoutTemplate"/>). An empty or invalid
    /// template is answered with its problems, not with 400.
    /// </summary>
    [Required(AllowEmptyStrings = true)]
    [StringLength(4096)]
    public required string Template { get; init; }

    /// <summary>
    /// Tracks of the library to show, at most 10; unknown ids are left out.
    /// </summary>
    [MaxLength(10)]
    public IReadOnlyList<Guid>? TrackIds { get; init; }

    /// <summary>
    /// Example songs to show after the tracks, at most 10.
    /// </summary>
    [MaxLength(10)]
    public IReadOnlyList<LayoutTemplateSampleDto>? Samples { get; init; }
}
