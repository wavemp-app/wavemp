using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using WaveMP.Core.Themes.Definitions;
using WaveMP.Core.Themes.Storage;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Policies;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The files of the uploaded themes: the CSS file, the pictures and the fonts. The browser loads them through a link
/// element and through the CSS itself, which cannot send a session token, so they need no session; they hold nothing
/// private.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route(ThemeFiles.Route)]
public sealed class ThemeFilesController(IThemeStore store) : ControllerBase
{
    /// <summary>
    /// Cache-Control of the files: a file is named after its content, so its address never shows another content.
    /// </summary>
    public const string CacheControl = "public, max-age=31536000, immutable";

    /// <summary>
    /// Returns a file of an uploaded theme with its content type, as <c>stylesheetUrl</c> and <c>previews</c> of the
    /// theme and the addresses in its CSS name it. The name of every file is a hash of its content, so the response is
    /// cached for a year (Cache-Control: public, max-age=31536000, immutable) and carries the hash as strong ETag; a
    /// request with a matching If-None-Match returns 304.
    /// </summary>
    /// <param name="id">Id of the theme.</param>
    /// <param name="name">Name of the file: 32 hexadecimal digits and the extension css, svg, png, webp or
    /// woff2.</param>
    /// <response code="200">The file.</response>
    /// <response code="404">The theme does not exist, has no such file or is blocked because it breaks a security
    /// rule of this version (code <c>theme.file_not_found</c>).</response>
    [HttpGet(Name = "GetThemeFile")]
    [ProducesResponseType<Stream>(
        StatusCodes.Status200OK,
        "text/css",
        ThemeFiles.SvgContentType,
        ThemeFiles.PngContentType,
        ThemeFiles.WebPContentType,
        ThemeFiles.Woff2ContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult GetThemeFile(string id, string name)
    {
        Stream? content = ThemeFiles.IsValidName(name) ? store.OpenFile(id, name) : null;
        if (content is null)
        {
            return this.Problem(ProblemCodeDto.ThemeFileNotFound, StatusCodes.Status404NotFound);
        }

        IHeaderDictionary headers = Response.Headers;
        headers.CacheControl = CacheControl;
        headers.XContentTypeOptions = "nosniff";
        headers.ContentSecurityPolicy = ContentSecurityPolicies.ThemeFile;
        return File(
            content,
            ThemeFiles.ContentTypeOf(name),
            lastModified: null,
            new EntityTagHeaderValue($"\"{Path.GetFileNameWithoutExtension(name)}\""));
    }
}
