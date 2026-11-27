namespace WaveMP.Service.Dtos;

/// <summary>
/// An artist of a song as its tags name it, with its role.
/// </summary>
public sealed class ArtistNameDto
{
    public required string Name { get; init; }

    /// <summary>
    /// Main or featured artist.
    /// </summary>
    public required ArtistRoleDto Role { get; init; }
}
