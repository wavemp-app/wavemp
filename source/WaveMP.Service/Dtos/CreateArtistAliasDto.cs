using System.ComponentModel.DataAnnotations;
using WaveMP.Core.Library.Artists;

namespace WaveMP.Service.Dtos;

/// <summary>
/// Adds an alias to an artist.
/// </summary>
public sealed class CreateArtistAliasDto
{
    /// <summary>
    /// The alias, 1 to 512 characters; white space is joined and removed at the ends.
    /// </summary>
    [Required]
    [StringLength(IArtistAliasService.MaxNameLength)]
    public required string Name { get; init; }
}
