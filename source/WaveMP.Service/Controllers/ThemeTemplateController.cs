using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using WaveMP.Core.Themes.Templates;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The template for own themes: an example theme with a commented CSS file, a manifest and pictures, ready to change
/// and upload (see THEMING.md). It needs no session, so a plain download link works.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/v1/theme-template")]
public sealed class ThemeTemplateController : ControllerBase
{
    private static readonly Lazy<EntityTagHeaderValue> _entityTag = new(() => new EntityTagHeaderValue(
        $"\"{Convert.ToHexStringLower(SHA256.HashData(ThemeTemplate.Zip).AsSpan(0, 16))}\""));

    /// <summary>
    /// Returns the template as ZIP file (<c>wavemp-theme-template.zip</c>), written for the theme API of this
    /// server. Carries a strong ETag; a request with a matching If-None-Match returns 304.
    /// </summary>
    /// <response code="200">The template.</response>
    [HttpGet(Name = "GetThemeTemplate")]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, ThemesController.ZipContentType)]
    public IActionResult GetThemeTemplate()
    {
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(
            ThemeTemplate.Zip,
            ThemesController.ZipContentType,
            ThemeTemplate.FileName,
            lastModified: null,
            _entityTag.Value);
    }
}
