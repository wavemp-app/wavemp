namespace WaveMP.Service.Dtos;

/// <summary>
/// An artist of a song or album, in the order of its artists, with its role.
/// </summary>
public sealed class ArtistCreditDto
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    /// <summary>
    /// Main or featured artist; always Main for album artists.
    /// </summary>
    public required ArtistRoleDto Role { get; init; }
}
