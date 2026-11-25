using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Organizing.Operations;
using WaveMP.Core.Organizing.Previews;
using WaveMP.Data.Entities;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Queries;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// Organization of the media files by the layout template, for admins only: previews of own albums, of the library and
/// of undos, their confirmation and cancellation, and the log of the organize operations. A preview never touches a
/// file; only a confirmed operation moves files, by the job library-organize.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/organize")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class OrganizeController(
    ITemplatePreviewService templatePreview,
    IOrganizePreviewService previews,
    IOrganizeService organize) : ControllerBase
{
    private const string OperationRoute = "GetOrganizeOperation";

    /// <summary>
    /// Shows where a layout template would put songs of the library or example songs, without saving the template and
    /// without touching any file. An invalid template is answered with its problems (codes with their position).
    /// </summary>
    /// <param name="request">The template and the songs to show.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The preview: whether the template is valid, its problems and the paths of the
    /// songs.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>, errors by field).</response>
    [HttpPost("template-preview", Name = "PreviewLayoutTemplate")]
    [Consumes("application/json")]
    [ProducesResponseType<LayoutTemplatePreviewDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<LayoutTemplatePreviewDto>> PreviewLayoutTemplateAsync(
        PreviewLayoutTemplateDto request,
        CancellationToken cancellationToken)
    {
        TemplatePreviewResult result = await templatePreview.PreviewAsync(request.ToRequest(), cancellationToken);
        return Ok(result.ToDto());
    }

    /// <summary>
    /// Whether the files can be organized: per media source (a read-only mount answers ReadOnly), and the operation
    /// that is queued or running.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The availability.</response>
    [HttpGet("availability", Name = "GetOrganizeAvailability")]
    [ProducesResponseType<OrganizeAvailabilityDto>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<OrganizeAvailabilityDto>> GetOrganizeAvailabilityAsync(
        CancellationToken cancellationToken)
    {
        OrganizeAvailability availability = await organize.GetAvailabilityAsync(cancellationToken);
        return Ok(availability.ToDto());
    }

    /// <summary>
    /// Computes the preview of an organize operation by the saved layout template: for an own album (its tags and
    /// files) or for the library (all songs, or those of one media source). Nothing on disk changes. The preview can
    /// be applied until expiresAt. A library preview is returned in the state Planning: the job library-organize plans
    /// it (totalItems grows meanwhile, CancelOrganizeOperation stops it) and it becomes Previewed.
    /// </summary>
    /// <param name="request">What to organize.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="201">The preview; Location points to the operation, its items list the changes.</response>
    /// <response code="400">The request is invalid, e.g. the kind OwnAlbum without ownAlbumId (code
    /// <c>request.invalid</c>).</response>
    /// <response code="404">The own album (code <c>organize.own_album_not_found</c>) or the media source (code
    /// <c>source.not_found</c>) does not exist.</response>
    /// <response code="409">The own album has no songs (code <c>organize.own_album_empty</c>), a media folder cannot be
    /// written (code <c>organize.media_read_only</c>), the saved template cannot be used (code
    /// <c>organize.template_invalid</c>) or another operation is queued or running or the organize job is busy (code
    /// <c>organize.busy</c>).</response>
    [HttpPost("preview", Name = "CreateOrganizePreview")]
    [Consumes("application/json")]
    [ProducesResponseType<OrganizeOperationDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OrganizeOperationDto>> CreateOrganizePreviewAsync(
        CreateOrganizePreviewDto request,
        CancellationToken cancellationToken)
    {
        if (request.Kind == OrganizePreviewKindDto.OwnAlbum && request.OwnAlbumId is null)
        {
            ModelState.AddValidationError("ownAlbumId", ValidationErrorCodeDto.Required);
            return ValidationProblem();
        }

        OrganizePreviewResult result = request.Kind == OrganizePreviewKindDto.OwnAlbum
            ? await previews.PreviewOwnAlbumAsync(request.OwnAlbumId!.Value, User.GetUserId(), cancellationToken)
            : await previews.PreviewLibraryAsync(request.SourceId, User.GetUserId(), cancellationToken);
        return Created(result);
    }

    /// <summary>
    /// Computes the preview of undoing an organize operation: the files move back and changed tags are restored,
    /// unless they were changed again since. Nothing on disk changes.
    /// </summary>
    /// <param name="id">Id of the operation to undo.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="201">The preview of the undo; Location points to it.</response>
    /// <response code="404">The operation does not exist (code <c>organize.operation_not_found</c>).</response>
    /// <response code="409">The operation cannot be undone (code <c>organize.not_undoable</c>), was undone already
    /// (code <c>organize.already_undone</c>), a media folder cannot be written (code
    /// <c>organize.media_read_only</c>) or another operation is queued or running (code
    /// <c>organize.busy</c>).</response>
    [HttpPost("undo/{id:guid}", Name = "CreateOrganizeUndoPreview")]
    [ProducesResponseType<OrganizeOperationDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OrganizeOperationDto>> CreateOrganizeUndoPreviewAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        OrganizePreviewResult result = await previews.PreviewUndoAsync(id, User.GetUserId(), cancellationToken);
        return Created(result);
    }

    /// <summary>
    /// Confirms a preview: the organize job (library-organize) moves the files, writes the tags into the audio files
    /// (after a backup) and records every step before it happens, so the operation can be undone and a stop of the
    /// server in the middle is put back on the next start. Every song is checked against the disk again right before
    /// it moves; a song that changed since the preview stops the run (status Stopped, the failed item names its
    /// conflict). No file is ever replaced. Follow the progress with GetOrganizeOperation.
    /// </summary>
    /// <param name="request">The preview and whether its conflicts are skipped.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="202">The operation is queued; Location points to it.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>).</response>
    /// <response code="404">The operation does not exist (code <c>organize.operation_not_found</c>).</response>
    /// <response code="409">The operation is no preview waiting for its confirmation (code
    /// <c>organize.not_previewed</c>), the preview expired (code <c>organize.preview_expired</c>), it has conflicts and
    /// skipConflicts is not set (code <c>organize.conflicts</c>), the template, the own album or the undone operation
    /// changed since the preview (code <c>organize.preview_outdated</c>), a media folder cannot be written (code
    /// <c>organize.media_read_only</c>) or another operation is queued or running (code
    /// <c>organize.busy</c>).</response>
    [HttpPost("apply", Name = "ApplyOrganizeOperation")]
    [Consumes("application/json")]
    [ProducesResponseType<OrganizeOperationDto>(StatusCodes.Status202Accepted, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OrganizeOperationDto>> ApplyOrganizeOperationAsync(
        ApplyOrganizeOperationDto request,
        CancellationToken cancellationToken)
    {
        OrganizeApplyResult result = await organize.ApplyAsync(
            request.OperationId!.Value,
            request.SkipConflicts,
            cancellationToken);
        return result.Status switch
        {
            OrganizeApplyStatus.Queued => AcceptedAtRoute(
                OperationRoute,
                new { id = result.Operation!.Id },
                result.Operation.ToDto()),
            OrganizeApplyStatus.NotPreviewed => this.Problem(
                ProblemCodeDto.OrganizeNotPreviewed,
                StatusCodes.Status409Conflict),
            OrganizeApplyStatus.PreviewExpired => this.Problem(
                ProblemCodeDto.OrganizePreviewExpired,
                StatusCodes.Status409Conflict),
            OrganizeApplyStatus.Conflicts => this.Problem(
                ProblemCodeDto.OrganizeConflicts,
                StatusCodes.Status409Conflict),
            OrganizeApplyStatus.PreviewOutdated => this.Problem(
                ProblemCodeDto.OrganizePreviewOutdated,
                StatusCodes.Status409Conflict),
            OrganizeApplyStatus.MediaReadOnly => this.Problem(
                ProblemCodeDto.OrganizeMediaReadOnly,
                StatusCodes.Status409Conflict),
            OrganizeApplyStatus.Busy => this.Problem(ProblemCodeDto.OrganizeBusy, StatusCodes.Status409Conflict),
            _ => OperationNotFound(),
        };
    }

    /// <summary>
    /// Asks an organize operation that is planning, queued or running to stop. A run stops between two songs, never in
    /// the middle of one; the songs done before stay done and can be undone. The operation ends as Canceled.
    /// </summary>
    /// <param name="id">Id of the operation.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="202">The operation stops soon; Location points to it.</response>
    /// <response code="404">The operation does not exist (code <c>organize.operation_not_found</c>).</response>
    /// <response code="409">The operation is not planning, queued or running (code
    /// <c>organize.not_cancelable</c>).</response>
    [HttpPost("operations/{id:guid}/cancel", Name = "CancelOrganizeOperation")]
    [ProducesResponseType<OrganizeOperationDto>(StatusCodes.Status202Accepted, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OrganizeOperationDto>> CancelOrganizeOperationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        OrganizeCancelResult result = await organize.CancelAsync(id, cancellationToken);
        return result switch
        {
            { Status: OrganizeCancelStatus.Requested, Operation: { } operation } => AcceptedAtRoute(
                OperationRoute,
                new { id },
                operation.ToDto()),
            { Status: OrganizeCancelStatus.NotCancelable } => this.Problem(
                ProblemCodeDto.OrganizeNotCancelable,
                StatusCodes.Status409Conflict),
            _ => OperationNotFound(),
        };
    }

    /// <summary>
    /// Lists the organize operations, newest first: previews, runs and undos.
    /// </summary>
    /// <param name="offset">Number of operations to skip.</param>
    /// <param name="limit">Maximum number of operations of the page.</param>
    /// <param name="kind">Only operations of this kind.</param>
    /// <param name="status">Only operations in this state.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">One page of operations.</response>
    /// <response code="400">Invalid query parameters (code <c>request.invalid</c>).</response>
    [HttpGet("operations", Name = "ListOrganizeOperations")]
    [ProducesResponseType<PageDto<OrganizeOperationDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PageDto<OrganizeOperationDto>>> ListOrganizeOperationsAsync(
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit,
        [FromQuery] OrganizeOperationKindDto? kind = null,
        [FromQuery] OrganizeOperationStatusDto? status = null,
        CancellationToken cancellationToken = default)
    {
        this.ValidateEnum(kind, nameof(kind));
        this.ValidateEnum(status, nameof(status));
        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        Page<OrganizeOperation> page = await organize.ListOperationsAsync(
            new OrganizeOperationListQuery(offset, limit, kind?.ToEntity(), status?.ToEntity()),
            cancellationToken);
        return Ok(page.ToDto(operation => operation.ToDto()));
    }

    /// <summary>
    /// Returns an organize operation.
    /// </summary>
    /// <param name="id">Id of the operation.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The operation.</response>
    /// <response code="404">The operation does not exist (code <c>organize.operation_not_found</c>).</response>
    [HttpGet("operations/{id:guid}", Name = OperationRoute)]
    [ProducesResponseType<OrganizeOperationDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OrganizeOperationDto>> GetOrganizeOperationAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        OrganizeOperation? operation = await organize.GetOperationAsync(id, cancellationToken);
        return operation is null ? OperationNotFound() : Ok(operation.ToDto());
    }

    /// <summary>
    /// Lists the items of an organize operation in their order, all or only those with a conflict or a warning.
    /// </summary>
    /// <param name="id">Id of the operation.</param>
    /// <param name="offset">Number of items to skip.</param>
    /// <param name="limit">Maximum number of items of the page.</param>
    /// <param name="filter">Which items; all by default.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">One page of items.</response>
    /// <response code="400">Invalid query parameters (code <c>request.invalid</c>).</response>
    /// <response code="404">The operation does not exist (code <c>organize.operation_not_found</c>).</response>
    [HttpGet("operations/{id:guid}/items", Name = "ListOrganizeItems")]
    [ProducesResponseType<PageDto<OrganizeItemDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PageDto<OrganizeItemDto>>> ListOrganizeItemsAsync(
        Guid id,
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit,
        [FromQuery] OrganizeItemFilterDto filter = OrganizeItemFilterDto.All,
        CancellationToken cancellationToken = default)
    {
        this.ValidateEnum<OrganizeItemFilterDto>(filter, nameof(filter));
        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        Page<OrganizeItem>? page = await organize.ListItemsAsync(
            new OrganizeItemListQuery(id, offset, limit, filter.ToEntity()),
            cancellationToken);
        return page is null ? OperationNotFound() : Ok(page.ToDto(item => item.ToDto()));
    }

    /// <summary>
    /// Discards a preview (also an expired one). Operations that ran stay in the log.
    /// </summary>
    /// <param name="id">Id of the operation.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The preview was discarded.</response>
    /// <response code="404">The operation does not exist (code <c>organize.operation_not_found</c>).</response>
    /// <response code="409">The operation is no preview (code <c>organize.not_preview</c>).</response>
    [HttpDelete("operations/{id:guid}", Name = "DeleteOrganizeOperation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> DeleteOrganizeOperationAsync(Guid id, CancellationToken cancellationToken)
    {
        return await organize.DiscardAsync(id, cancellationToken) switch
        {
            OrganizeDiscardStatus.Discarded => NoContent(),
            OrganizeDiscardStatus.NotPreview => this.Problem(
                ProblemCodeDto.OrganizeNotPreview,
                StatusCodes.Status409Conflict),
            _ => OperationNotFound(),
        };
    }

    private ObjectResult OperationNotFound()
    {
        return this.Problem(ProblemCodeDto.OrganizeOperationNotFound, StatusCodes.Status404NotFound);
    }

    private ActionResult<OrganizeOperationDto> Created(OrganizePreviewResult result)
    {
        if (result is { Status: OrganizePreviewStatus.Created, Operation: { } operation })
        {
            return CreatedAtRoute(OperationRoute, new { id = operation.Id }, operation.ToDto());
        }

        return result.Status switch
        {
            OrganizePreviewStatus.OwnAlbumNotFound => this.Problem(
                ProblemCodeDto.OwnAlbumNotFound,
                StatusCodes.Status404NotFound),
            OrganizePreviewStatus.SourceNotFound => this.Problem(
                ProblemCodeDto.SourceNotFound,
                StatusCodes.Status404NotFound),
            OrganizePreviewStatus.AlbumEmpty => this.Problem(
                ProblemCodeDto.OwnAlbumEmpty,
                StatusCodes.Status409Conflict),
            OrganizePreviewStatus.MediaReadOnly => this.Problem(
                ProblemCodeDto.OrganizeMediaReadOnly,
                StatusCodes.Status409Conflict),
            OrganizePreviewStatus.TemplateInvalid => this.Problem(
                ProblemCodeDto.OrganizeTemplateInvalid,
                StatusCodes.Status409Conflict),
            OrganizePreviewStatus.Busy => this.Problem(ProblemCodeDto.OrganizeBusy, StatusCodes.Status409Conflict),
            OrganizePreviewStatus.NotUndoable => this.Problem(
                ProblemCodeDto.OrganizeNotUndoable,
                StatusCodes.Status409Conflict),
            OrganizePreviewStatus.AlreadyUndone => this.Problem(
                ProblemCodeDto.OrganizeAlreadyUndone,
                StatusCodes.Status409Conflict),
            _ => OperationNotFound(),
        };
    }
}
