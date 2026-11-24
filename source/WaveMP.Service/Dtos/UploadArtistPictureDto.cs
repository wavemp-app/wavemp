using Microsoft.AspNetCore.Mvc;

namespace WaveMP.Service.Dtos;

/// <summary>
/// An uploaded artist picture as multipart/form-data.
/// </summary>
public sealed class UploadArtistPictureDto
{
    /// <summary>
    /// A JPEG, PNG or WebP picture (10 MiB at most by default). The server cuts the largest square out of its middle,
    /// so clients that let the admin crop the picture upload the cropped square.
    /// </summary>
    [FromForm(Name = "file")]
    public IFormFile? File { get; init; }
}
