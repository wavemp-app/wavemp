using System.ComponentModel.DataAnnotations;
using WaveMP.Service.Queries;

namespace WaveMP.Service.Dtos;

/// <summary>
/// An album artist as the metadata editor writes it: an artist of the library or a new name.
/// </summary>
public sealed class AlbumArtistInputDto
{
    /// <summary>
    /// Name of the artist; a name whose normalized form matches an artist of the library refers to that artist.
    /// </summary>
    [Required]
    [StringLength(LibraryLimits.NameMaxLength)]
    public required string Name { get; init; }
}
