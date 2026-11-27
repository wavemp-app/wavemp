using System.ComponentModel.DataAnnotations;
using WaveMP.Service.Queries;

namespace WaveMP.Service.Dtos;

/// <summary>
/// An artist of a song as the metadata editor writes it: an artist of the library or a new name.
/// </summary>
public sealed class ArtistCreditInputDto
{
    /// <summary>
    /// Name of the artist; a name whose normalized form matches an artist of the library refers to that artist.
    /// </summary>
    [Required]
    [StringLength(LibraryLimits.NameMaxLength)]
    public required string Name { get; init; }

    /// <summary>
    /// Main or featured artist; Main if missing.
    /// </summary>
    public ArtistRoleDto Role { get; init; } = ArtistRoleDto.Main;
}
