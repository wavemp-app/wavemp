using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Services;
using WaveMP.Core.Settings.Validations;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// Settings of the server, for admins only. Each group of settings is a resource of its own.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/settings")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class SettingsController(
    ILibrarySettingsService librarySettings,
    ICacheSettingsService cacheSettings,
    ITranscodingSettingsService transcodingSettings,
    IBackupSettingsService backupSettings,
    IOnlineMetadataSettingsService onlineMetadataSettings) : ControllerBase
{
    /// <summary>
    /// Returns the settings of the media library.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The library settings.</response>
    [HttpGet("library", Name = "GetLibrarySettings")]
    [ProducesResponseType<LibrarySettingsDto>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<LibrarySettingsDto>> GetLibrarySettingsAsync(CancellationToken cancellationToken)
    {
        LibrarySettings settings = await librarySettings.GetAsync(cancellationToken);
        return Ok(settings.ToDto());
    }

    /// <summary>
    /// Changes the settings of the media library. Missing or null values stay unchanged. A new scan interval and the
    /// file watcher apply at once, the lists with the next scan.
    /// </summary>
    /// <param name="request">The changes.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The changed settings, normalized (e.g. extensions in lower case without dot).</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>, errors by field).</response>
    [HttpPatch("library", Name = "UpdateLibrarySettings")]
    [Consumes("application/json")]
    [ProducesResponseType<LibrarySettingsDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<LibrarySettingsDto>> UpdateLibrarySettingsAsync(
        UpdateLibrarySettingsDto request,
        CancellationToken cancellationToken)
    {
        LibrarySettingsResult result = await librarySettings.UpdateAsync(request.ToCommand(), cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(result.Settings.ToDto());
        }

        foreach (SettingsValidationError error in result.Errors)
        {
            ModelState.AddValidationError(
                error.Field.ToJsonName(),
                error.Reason.ToCode(),
                error.Minimum,
                error.Maximum);
        }

        return ValidationProblem();
    }

    /// <summary>
    /// Returns the settings of the cache.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The cache settings.</response>
    [HttpGet("cache", Name = "GetCacheSettings")]
    [ProducesResponseType<CacheSettingsDto>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<CacheSettingsDto>> GetCacheSettingsAsync(CancellationToken cancellationToken)
    {
        CacheSettings settings = await cacheSettings.GetAsync(cancellationToken);
        return Ok(settings.ToDto());
    }

    /// <summary>
    /// Changes the settings of the cache. Missing or null values stay unchanged. A lower size limit cleans up the cache
    /// in the background at once.
    /// </summary>
    /// <param name="request">The changes.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The changed settings.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>, errors by field).</response>
    [HttpPatch("cache", Name = "UpdateCacheSettings")]
    [Consumes("application/json")]
    [ProducesResponseType<CacheSettingsDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<CacheSettingsDto>> UpdateCacheSettingsAsync(
        UpdateCacheSettingsDto request,
        CancellationToken cancellationToken)
    {
        CacheSettingsResult result = await cacheSettings.UpdateAsync(request.ToCommand(), cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(result.Settings.ToDto());
        }

        foreach (CacheSettingsValidationError error in result.Errors)
        {
            ModelState.AddValidationError(
                error.Field.ToJsonName(),
                error.Reason.ToCode(),
                error.Minimum,
                error.Maximum);
        }

        return ValidationProblem();
    }

    /// <summary>
    /// Returns the settings of the transcoding.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The transcoding settings.</response>
    [HttpGet("transcoding", Name = "GetTranscodingSettings")]
    [ProducesResponseType<TranscodingSettingsDto>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<TranscodingSettingsDto>> GetTranscodingSettingsAsync(
        CancellationToken cancellationToken)
    {
        TranscodingSettings settings = await transcodingSettings.GetAsync(cancellationToken);
        return Ok(settings.ToDto());
    }

    /// <summary>
    /// Changes the settings of the transcoding. Missing or null values stay unchanged. New transcodes use the new
    /// values at once, running ones keep theirs; a change of the hardware acceleration starts a new detection of the
    /// graphics hardware in the background.
    /// </summary>
    /// <param name="request">The changes.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The changed settings.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>, errors by field).</response>
    [HttpPatch("transcoding", Name = "UpdateTranscodingSettings")]
    [Consumes("application/json")]
    [ProducesResponseType<TranscodingSettingsDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<TranscodingSettingsDto>> UpdateTranscodingSettingsAsync(
        UpdateTranscodingSettingsDto request,
        CancellationToken cancellationToken)
    {
        TranscodingSettingsResult result = await transcodingSettings.UpdateAsync(
            request.ToCommand(),
            cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(result.Settings.ToDto());
        }

        foreach (TranscodingSettingsValidationError error in result.Errors)
        {
            ModelState.AddValidationError(
                error.Field.ToJsonName(),
                error.Reason.ToCode(),
                error.Minimum,
                error.Maximum);
        }

        return ValidationProblem();
    }

    /// <summary>
    /// Returns the settings of the database backups.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The backup settings.</response>
    [HttpGet("backup", Name = "GetBackupSettings")]
    [ProducesResponseType<BackupSettingsDto>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<BackupSettingsDto>> GetBackupSettingsAsync(CancellationToken cancellationToken)
    {
        BackupSettings settings = await backupSettings.GetAsync(cancellationToken);
        return Ok(settings.ToDto());
    }

    /// <summary>
    /// Changes the settings of the database backups. Missing or null values stay unchanged. A new time of day plans
    /// the daily backup again at once; a lower number of kept exports applies with the next export.
    /// </summary>
    /// <param name="request">The changes.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The changed settings.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>, errors by field).</response>
    [HttpPatch("backup", Name = "UpdateBackupSettings")]
    [Consumes("application/json")]
    [ProducesResponseType<BackupSettingsDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<BackupSettingsDto>> UpdateBackupSettingsAsync(
        UpdateBackupSettingsDto request,
        CancellationToken cancellationToken)
    {
        BackupSettingsResult result = await backupSettings.UpdateAsync(request.ToCommand(), cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(result.Settings.ToDto());
        }

        foreach (BackupSettingsValidationError error in result.Errors)
        {
            ModelState.AddValidationError(
                error.Field.ToJsonName(),
                error.Reason.ToCode(),
                error.Minimum,
                error.Maximum);
        }

        return ValidationProblem();
    }

    /// <summary>
    /// Returns the settings of the online search for album info (MusicBrainz and the Cover Art Archive).
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The settings of the online search.</response>
    [HttpGet("online-metadata", Name = "GetOnlineMetadataSettings")]
    [ProducesResponseType<OnlineMetadataSettingsDto>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<OnlineMetadataSettingsDto>> GetOnlineMetadataSettingsAsync(
        CancellationToken cancellationToken)
    {
        OnlineMetadataSettings settings = await onlineMetadataSettings.GetAsync(cancellationToken);
        return Ok(settings.ToDto());
    }

    /// <summary>
    /// Changes the settings of the online search for album info. Missing or null values stay unchanged; changes
    /// apply to the next request. While the search is off, WaveMP sends no request to either service.
    /// </summary>
    /// <param name="request">The changes.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The changed settings.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>, errors by field: <c>invalid_format</c> for
    /// an address that is not an absolute http or https address without query, <c>out_of_range</c>).</response>
    [HttpPatch("online-metadata", Name = "UpdateOnlineMetadataSettings")]
    [Consumes("application/json")]
    [ProducesResponseType<OnlineMetadataSettingsDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<OnlineMetadataSettingsDto>> UpdateOnlineMetadataSettingsAsync(
        UpdateOnlineMetadataSettingsDto request,
        CancellationToken cancellationToken)
    {
        OnlineMetadataSettingsResult result =
            await onlineMetadataSettings.UpdateAsync(request.ToCommand(), cancellationToken);
        if (result.IsSuccess)
        {
            return Ok(result.Settings.ToDto());
        }

        foreach (OnlineMetadataSettingsValidationError error in result.Errors)
        {
            ModelState.AddValidationError(
                error.Field.ToJsonName(),
                error.Reason.ToCode(),
                error.Minimum,
                error.Maximum);
        }

        return ValidationProblem();
    }
}
