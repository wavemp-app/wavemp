namespace WaveMP.Service.Dtos;

/// <summary>
/// The avatar of a profile: an uploaded picture or a built-in avatar. Its picture comes from
/// GET /api/v1/users/{id}/avatar; profiles without an avatar have none, and clients show their initials.
/// </summary>
public sealed class AvatarDto
{
    /// <summary>
    /// Version of the avatar; it changes every time the avatar is set, so clients can use it as part of their cache
    /// key.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Id of the built-in avatar (see GET /api/v1/avatar-presets); null for an uploaded picture.
    /// </summary>
    public string? PresetId { get; init; }
}
