using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Scanning.Sources;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Queries;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The media sources: folders of the container whose media files the library contains. Every profile can list them
/// (e.g. to filter the library by source); only admins add, change and remove them.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/sources")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class MediaSourcesController(IMediaSourceService sources) : ControllerBase
{
    private const string GetSourceRoute = "GetMediaSource";

    /// <summary>
    /// Lists the media sources, sorted by name.
    /// </summary>
    /// <param name="offset">Number of sources to skip.</param>
    /// <param name="limit">Maximum number of sources of the page.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">One page of media sources.</response>
    /// <response code="400">Invalid query parameters (code <c>request.invalid</c>).</response>
    [HttpGet(Name = "ListMediaSources")]
    [ProducesResponseType<PageDto<MediaSourceDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PageDto<MediaSourceDto>>> ListMediaSourcesAsync(
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MediaSourceOverview> all = await sources.ListAsync(cancellationToken);
        return Ok(new PageDto<MediaSourceDto>
        {
            Items = [.. all.Skip(offset).Take(limit).Select(source => source.ToDto())],
            Offset = offset,
            Limit = limit,
            Total = all.Count,
        });
    }

    /// <summary>
    /// Returns a media source.
    /// </summary>
    /// <param name="id">Id of the source.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The source.</response>
    /// <response code="404">The source does not exist (code <c>source.not_found</c>).</response>
    [HttpGet("{id:guid}", Name = GetSourceRoute)]
    [ProducesResponseType<MediaSourceDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MediaSourceDto>> GetMediaSourceAsync(Guid id, CancellationToken cancellationToken)
    {
        MediaSourceOverview? source = await sources.GetAsync(id, cancellationToken);
        return source is null
            ? this.Problem(ProblemCodeDto.SourceNotFound, StatusCodes.Status404NotFound)
            : Ok(source.ToDto());
    }

    /// <summary>
    /// Adds a media source. An active source is scanned at once, or after a running scan.
    /// </summary>
    /// <param name="request">The new source.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="201">The added source; Location points to it.</response>
    /// <response code="400">The request is invalid, e.g. a relative path (code <c>request.invalid</c>).</response>
    /// <response code="403">The profile is not an admin (code <c>auth.forbidden</c>).</response>
    /// <response code="409">The name is taken (code <c>source.name_taken</c>) or the folder overlaps the folder of
    /// another source (code <c>source.path_overlaps</c>).</response>
    /// <response code="422">The folder does not exist (code <c>source.path_not_found</c>).</response>
    [HttpPost(Name = "CreateMediaSource")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [Consumes("application/json")]
    [ProducesResponseType<MediaSourceDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<MediaSourceDto>> CreateMediaSourceAsync(
        CreateMediaSourceDto request,
        CancellationToken cancellationToken)
    {
        MediaSourceResult result = await sources.CreateAsync(
            new CreateMediaSourceCommand(request.Name, request.Path, request.IsActive),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return Failure(result.Error!.Value);
        }

        return CreatedAtRoute(GetSourceRoute, new { id = result.Source.Id }, result.Source.ToDto());
    }

    /// <summary>
    /// Changes a media source. Missing or null values stay unchanged. A new folder or a reactivated source is scanned
    /// at once, or after a running scan; a deactivated source is no longer scanned or watched, its tracks stay.
    /// </summary>
    /// <param name="id">Id of the source.</param>
    /// <param name="request">The changes.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The changed source.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>).</response>
    /// <response code="403">The profile is not an admin (code <c>auth.forbidden</c>).</response>
    /// <response code="404">The source does not exist (code <c>source.not_found</c>).</response>
    /// <response code="409">The name is taken (code <c>source.name_taken</c>), the folder overlaps the folder of
    /// another source (code <c>source.path_overlaps</c>) or a scan runs while the folder changes (code
    /// <c>scan.already_running</c>).</response>
    /// <response code="422">The folder does not exist (code <c>source.path_not_found</c>).</response>
    [HttpPatch("{id:guid}", Name = "UpdateMediaSource")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [Consumes("application/json")]
    [ProducesResponseType<MediaSourceDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    public async Task<ActionResult<MediaSourceDto>> UpdateMediaSourceAsync(
        Guid id,
        UpdateMediaSourceDto request,
        CancellationToken cancellationToken)
    {
        MediaSourceResult result = await sources.UpdateAsync(
            id,
            new UpdateMediaSourceCommand(request.Name, request.Path, request.IsActive),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Source.ToDto()) : Failure(result.Error!.Value);
    }

    /// <summary>
    /// Removes a media source with its tracks and files from the library; the files on disk stay untouched.
    /// </summary>
    /// <param name="id">Id of the source.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The source is removed.</response>
    /// <response code="403">The profile is not an admin (code <c>auth.forbidden</c>).</response>
    /// <response code="404">The source does not exist (code <c>source.not_found</c>).</response>
    /// <response code="409">A scan is queued or running (code <c>scan.already_running</c>).</response>
    [HttpDelete("{id:guid}", Name = "DeleteMediaSource")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> DeleteMediaSourceAsync(Guid id, CancellationToken cancellationToken)
    {
        MediaSourceError? error = await sources.DeleteAsync(id, cancellationToken);
        return error is { } value ? Failure(value) : NoContent();
    }

    private ActionResult Failure(MediaSourceError error)
    {
        return error switch
        {
            MediaSourceError.NotFound => this.Problem(ProblemCodeDto.SourceNotFound, StatusCodes.Status404NotFound),
            MediaSourceError.NameTaken => this.Problem(ProblemCodeDto.SourceNameTaken, StatusCodes.Status409Conflict),
            MediaSourceError.PathOverlaps => this.Problem(
                ProblemCodeDto.SourcePathOverlaps,
                StatusCodes.Status409Conflict),
            MediaSourceError.ScanRunning => this.Problem(
                ProblemCodeDto.ScanAlreadyRunning,
                StatusCodes.Status409Conflict),
            MediaSourceError.PathNotFound => this.Problem(
                ProblemCodeDto.SourcePathNotFound,
                StatusCodes.Status422UnprocessableEntity),
            MediaSourceError.InvalidName => Invalid("name", ValidationErrorCodeDto.InvalidValue),
            MediaSourceError.InvalidPath => Invalid("path", ValidationErrorCodeDto.InvalidFormat),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error, "Unknown error."),
        };
    }

    private ActionResult Invalid(string field, ValidationErrorCodeDto code)
    {
        ModelState.AddValidationError(field, code);
        return ValidationProblem();
    }
}
