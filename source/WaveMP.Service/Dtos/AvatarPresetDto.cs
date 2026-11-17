namespace WaveMP.Service.Dtos;

/// <summary>
/// A built-in avatar that profiles can choose instead of uploading a picture. Its picture comes from
/// GET /api/v1/avatar-presets/{id}/picture.
/// </summary>
public sealed class AvatarPresetDto
{
    /// <summary>
    /// Stable id of the built-in avatar, e.g. "bubble"; clients may translate it into a name.
    /// </summary>
    public required string Id { get; init; }
}
