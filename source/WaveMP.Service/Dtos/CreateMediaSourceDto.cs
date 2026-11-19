using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Adds a media source.
/// </summary>
public sealed class CreateMediaSourceDto
{
    /// <summary>
    /// Name of the source, unique without regard to case.
    /// </summary>
    [Required]
    [StringLength(100)]
    public required string Name { get; init; }

    /// <summary>
    /// Absolute path of an existing folder in the container, apart from the folders of the other sources (neither the
    /// same, nor inside one, nor containing one).
    /// </summary>
    [Required]
    [StringLength(1000)]
    public required string Path { get; init; }

    /// <summary>
    /// Whether the source is scanned and watched; true if missing.
    /// </summary>
    public bool? IsActive { get; init; }
}
