namespace WaveMP.Service.Dtos;

/// <summary>
/// A profile as shown in the profile selection.
/// </summary>
public sealed class ProfileDto
{
    public required Guid Id { get; init; }

    /// <summary>
    /// Name of the profile.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Initials shown on the profile tile.
    /// </summary>
    public required string Initials { get; init; }

    /// <summary>
    /// Color of the profile as #rrggbb.
    /// </summary>
    public required string Color { get; init; }

    public required RoleDto Role { get; init; }

    /// <summary>
    /// True for the built-in admin profile, which cannot be deleted and always stays an admin.
    /// </summary>
    public required bool IsBuiltIn { get; init; }

    /// <summary>
    /// True if the profile is protected by a PIN, which selecting it needs.
    /// </summary>
    public required bool HasPin { get; init; }

    /// <summary>
    /// The avatar of the profile; null without one, then clients show the initials on the color of the profile.
    /// </summary>
    public AvatarDto? Avatar { get; init; }
}
