using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Sets what an album shows without a cover of its own.
/// </summary>
public sealed class UpdateAlbumCoverFallbackDto
{
    /// <summary>
    /// The rule; Inherit removes the exception of the album.
    /// </summary>
    [Required]
    public required AlbumCoverFallbackOptionDto? Fallback { get; init; }
}
