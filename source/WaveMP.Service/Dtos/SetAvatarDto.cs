using Microsoft.AspNetCore.Mvc;

namespace WaveMP.Service.Dtos;

/// <summary>
/// The new avatar of a profile as multipart/form-data: either an uploaded picture in <c>file</c> or the id of a
/// built-in avatar in <c>presetId</c>, never both.
/// </summary>
public sealed class SetAvatarDto
{
    /// <summary>
    /// A JPEG, PNG or WebP picture (10 MiB at most by default). The server cuts the largest square out of its middle,
    /// so clients that let the user crop the picture upload the cropped square.
    /// </summary>
    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }

    /// <summary>
    /// Id of a built-in avatar (see GET /api/v1/avatar-presets).
    /// </summary>
    [FromForm(Name = "presetId")]
    public string? PresetId { get; init; }
}
