using System.ComponentModel.DataAnnotations;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Songs for an own album, in this order. Duplicates are ignored.
/// </summary>
public sealed class OwnAlbumTracksDto
{
    /// <summary>
    /// Ids of the songs; at most 1000.
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public required IReadOnlyList<Guid> TrackIds { get; init; }
}
