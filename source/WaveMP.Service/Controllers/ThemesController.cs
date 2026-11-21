using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WaveMP.Core.Themes.Options;
using WaveMP.Core.Themes.Services;
using WaveMP.Core.Themes.Storage;
using WaveMP.Core.Themes.Validations;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.OpenApiTransformers;
using WaveMP.Service.Problems;
using WaveMP.Service.Queries;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The themes admins uploaded, in addition to the themes shipped with the clients. A theme is a ZIP file with a
/// <c>manifest.json</c>, exactly one CSS file and SVG, PNG or WebP pictures (see THEMING.md and the template at
/// <c>/api/v1/theme-template</c>). Every profile can list the themes and choose one in its settings; only admins
/// install, replace and delete them.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/themes")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class ThemesController(IThemeService themes, IOptions<ThemeOptions> options) : ControllerBase
{
    /// <summary>
    /// Content type of a theme package.
    /// </summary>
    public const string ZipContentType = "application/zip";

    private const string GetThemeRoute = "GetTheme";
    private const string ProblemContentType = "application/problem+json";
    private const int BufferSize = 81_920;

    /// <summary>
    /// Lists the uploaded themes by name. Each one names whether it passes the checks of this version of the server
    /// (<c>status</c>); an outdated theme lists its <c>problems</c> and is still offered until an admin uploads a new
    /// version. A blocked theme (it breaks a security rule of this version) lists its <c>problems</c> as well, but its
    /// files are no longer served and clients show the default theme instead.
    /// </summary>
    /// <param name="offset">Number of themes to skip.</param>
    /// <param name="limit">Maximum number of themes of the page.</param>
    /// <response code="200">One page of uploaded themes.</response>
    /// <response code="400">Invalid paging parameters (code <c>request.invalid</c>).</response>
    [HttpGet(Name = "ListThemes")]
    [ProducesResponseType<PageDto<ThemeDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, ProblemContentType)]
    public ActionResult<PageDto<ThemeDto>> ListThemes(
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit)
    {
        IReadOnlyList<InstalledTheme> installed = themes.List();
        return Ok(new PageDto<ThemeDto>
        {
            Items = [.. installed.Skip(offset).Take(limit).Select(theme => theme.ToDto())],
            Offset = offset,
            Limit = limit,
            Total = installed.Count,
        });
    }

    /// <summary>
    /// Returns an uploaded theme.
    /// </summary>
    /// <param name="id">Id of the theme.</param>
    /// <response code="200">The theme.</response>
    /// <response code="404">There is no uploaded theme with this id (code <c>theme.not_found</c>).</response>
    [HttpGet("{id}", Name = GetThemeRoute)]
    [ProducesResponseType<ThemeDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemContentType)]
    public ActionResult<ThemeDto> GetTheme(string id)
    {
        return themes.Find(id) is { } theme
            ? Ok(theme.ToDto())
            : this.Problem(ProblemCodeDto.ThemeNotFound, StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Installs a theme package, sent as the raw bytes of a ZIP file (10 MiB at most by default). The package is
    /// checked completely: its structure and limits, the manifest and its theme API version, the CSS file (no
    /// <c>@import</c>, only relative paths to pictures of the package) and the pictures (SVG files are cleaned).
    /// A rejected package returns every problem at once. Admins only.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="201">The installed theme; Location names it.</response>
    /// <response code="403">The profile is no admin (code <c>auth.forbidden</c>).</response>
    /// <response code="409">A theme with the id of the manifest is already installed (code
    /// <c>theme.already_installed</c>); replace it with PUT instead.</response>
    /// <response code="413">The package is too large (code <c>theme.too_large</c>).</response>
    /// <response code="415">The upload is no ZIP file (code <c>theme.unsupported_format</c>).</response>
    /// <response code="422">The package was rejected (code <c>theme.invalid</c>, the reasons in
    /// <c>errors</c>).</response>
    /// <response code="503">The folder of the themes cannot be written (code <c>service.unavailable</c>).</response>
    [HttpPost(Name = "InstallTheme")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [BinaryRequestBody(ZipContentType)]
    [ProducesResponseType<ThemeDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ProblemContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, ProblemContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, ProblemContentType)]
    [ProducesResponseType<ThemeProblemDto>(StatusCodes.Status422UnprocessableEntity, ProblemContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ProblemContentType)]
    public async Task<IActionResult> InstallThemeAsync(CancellationToken cancellationToken)
    {
        await using MemoryStream? package = await ReadPackageAsync(cancellationToken);
        if (package is null)
        {
            return this.Problem(ProblemCodeDto.ThemeTooLarge, StatusCodes.Status413PayloadTooLarge);
        }

        ThemeInstallResult result = await themes.InstallAsync(package, cancellationToken);
        return result is { Status: ThemeInstallStatus.Installed, Theme: { } theme }
            ? CreatedAtRoute(GetThemeRoute, new { id = theme.Id }, theme.ToDto())
            : InstallProblem(result);
    }

    /// <summary>
    /// Replaces an uploaded theme with a new version of its package, sent as the raw bytes of a ZIP file; the id in
    /// its manifest must be the id of the theme. The package is checked like a new one. Profiles that chose the theme
    /// keep it and see the new version. Admins only.
    /// </summary>
    /// <param name="id">Id of the theme.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The replaced theme.</response>
    /// <response code="403">The profile is no admin (code <c>auth.forbidden</c>).</response>
    /// <response code="404">There is no uploaded theme with this id (code <c>theme.not_found</c>).</response>
    /// <response code="413">The package is too large (code <c>theme.too_large</c>).</response>
    /// <response code="415">The upload is no ZIP file (code <c>theme.unsupported_format</c>).</response>
    /// <response code="422">The package was rejected (code <c>theme.invalid</c>, the reasons in <c>errors</c>),
    /// e.g. because its manifest names another id (<c>manifest.id_mismatch</c>).</response>
    /// <response code="503">The folder of the themes cannot be written (code <c>service.unavailable</c>).</response>
    [HttpPut("{id}", Name = "ReplaceTheme")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [BinaryRequestBody(ZipContentType)]
    [ProducesResponseType<ThemeDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, ProblemContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, ProblemContentType)]
    [ProducesResponseType<ThemeProblemDto>(StatusCodes.Status422UnprocessableEntity, ProblemContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ProblemContentType)]
    public async Task<IActionResult> ReplaceThemeAsync(string id, CancellationToken cancellationToken)
    {
        if (themes.Find(id) is null)
        {
            return this.Problem(ProblemCodeDto.ThemeNotFound, StatusCodes.Status404NotFound);
        }

        await using MemoryStream? package = await ReadPackageAsync(cancellationToken);
        if (package is null)
        {
            return this.Problem(ProblemCodeDto.ThemeTooLarge, StatusCodes.Status413PayloadTooLarge);
        }

        ThemeInstallResult result = await themes.ReplaceAsync(id, package, cancellationToken);
        return result is { Status: ThemeInstallStatus.Replaced, Theme: { } theme }
            ? Ok(theme.ToDto())
            : InstallProblem(result);
    }

    /// <summary>
    /// Deletes an uploaded theme. Profiles that chose it get the default theme. Admins only.
    /// </summary>
    /// <param name="id">Id of the theme.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The theme is deleted.</response>
    /// <response code="403">The profile is no admin (code <c>auth.forbidden</c>).</response>
    /// <response code="404">There is no uploaded theme with this id (code <c>theme.not_found</c>).</response>
    [HttpDelete("{id}", Name = "DeleteTheme")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ProblemContentType)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ProblemContentType)]
    public async Task<IActionResult> DeleteThemeAsync(string id, CancellationToken cancellationToken)
    {
        return await themes.DeleteAsync(id, cancellationToken)
            ? NoContent()
            : this.Problem(ProblemCodeDto.ThemeNotFound, StatusCodes.Status404NotFound);
    }

    private ObjectResult InstallProblem(ThemeInstallResult result)
    {
        return result.Status switch
        {
            ThemeInstallStatus.Invalid => InvalidPackage(result.Errors ?? []),
            ThemeInstallStatus.UnsupportedFormat => this.Problem(
                ProblemCodeDto.ThemeUnsupportedFormat,
                StatusCodes.Status415UnsupportedMediaType),
            ThemeInstallStatus.AlreadyInstalled => this.Problem(
                ProblemCodeDto.ThemeAlreadyInstalled,
                StatusCodes.Status409Conflict),
            ThemeInstallStatus.NotFound => this.Problem(ProblemCodeDto.ThemeNotFound, StatusCodes.Status404NotFound),
            _ => this.Problem(ProblemCodeDto.ServiceUnavailable, StatusCodes.Status503ServiceUnavailable),
        };
    }

    /// <summary>
    /// 422 with a <see cref="ThemeProblemDto"/> that lists the reasons.
    /// </summary>
    private ObjectResult InvalidPackage(IReadOnlyList<ThemeError> errors)
    {
        ObjectResult defaults = this.Problem(ProblemCodeDto.ThemeInvalid, StatusCodes.Status422UnprocessableEntity);
        var details = (ProblemDetails)defaults.Value!;
        ThemeProblemDto problem = new()
        {
            Type = details.Type,
            Title = details.Title,
            Status = details.Status,
            Detail = details.Detail,
            Instance = details.Instance,
            Errors = [.. errors.Select(error => error.ToDto())],
        };
        foreach (KeyValuePair<string, object?> extension in details.Extensions)
        {
            problem.Extensions[extension.Key] = extension.Value;
        }

        problem.Extensions[ProblemCodes.ExtensionName] = ProblemCodeDto.ThemeInvalid;
        ObjectResult result = new(problem) { StatusCode = StatusCodes.Status422UnprocessableEntity };
        result.ContentTypes.Add(ProblemContentType);
        return result;
    }

    /// <summary>
    /// Reads the request body into memory, or returns null as soon as it is longer than
    /// <see cref="ThemeOptions.MaxUploadBytes"/>; a body without Content-Length (chunked) is checked while reading.
    /// </summary>
    private async Task<MemoryStream?> ReadPackageAsync(CancellationToken cancellationToken)
    {
        long maxBytes = options.Value.MaxUploadBytes;
        if (Request.ContentLength > maxBytes)
        {
            return null;
        }

        // The server limit of request bodies must not cut a package that the configured limit allows.
        if (HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } feature &&
            feature.MaxRequestBodySize < maxBytes + 1)
        {
            feature.MaxRequestBodySize = maxBytes + 1;
        }

        MemoryStream content = new();
        byte[] buffer = new byte[BufferSize];
        int read;
        while ((read = await Request.Body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (content.Length + read > maxBytes)
            {
                await content.DisposeAsync();
                return null;
            }

            content.Write(buffer, 0, read);
        }

        content.Position = 0;
        return content;
    }
}
