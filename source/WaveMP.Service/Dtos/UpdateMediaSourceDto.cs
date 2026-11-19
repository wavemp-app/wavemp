using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Changes a media source. Missing or null values stay unchanged.
/// </summary>
public sealed class UpdateMediaSourceDto
{
    /// <summary>
    /// New name, unique without regard to case.
    /// </summary>
    [StringLength(100, MinimumLength = 1)]
    public string? Name { get; init; }

    /// <summary>
    /// New absolute path of an existing folder in the container, apart from the folders of the other sources. The
    /// source is scanned again; files with the same relative path keep their tracks.
    /// </summary>
    [StringLength(1000, MinimumLength = 1)]
    public string? Path { get; init; }

    /// <summary>
    /// Whether the source is scanned and watched. A reactivated source is scanned at once.
    /// </summary>
    public bool? IsActive { get; init; }
}
