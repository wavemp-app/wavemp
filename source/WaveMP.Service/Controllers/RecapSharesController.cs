using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Recaps.Shares;
using WaveMP.Core.Recaps.Statistics;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Queries;

namespace WaveMP.Service.Controllers;

/// <summary>
/// Recaps the profiles share with the other profiles of the instance. Nothing is shared unless a profile shares a
/// recap itself; every profile sees all recaps the others shared, with the statistics of the profile that shared them.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/recap-shares")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class RecapSharesController(IRecapShareService shares) : ControllerBase
{
    private const string GetShareRoute = "GetRecapShare";

    /// <summary>
    /// Lists the recaps the other profiles shared (scope received) or the current profile shared (scope own), the most
    /// recently shared first.
    /// </summary>
    /// <param name="scope">Which recaps to list; received without it.</param>
    /// <param name="offset">Number of shares to skip.</param>
    /// <param name="limit">Maximum number of shares of the page.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">One page of shared recaps.</response>
    /// <response code="400">Invalid query parameters (code <c>request.invalid</c>).</response>
    [HttpGet(Name = "ListRecapShares")]
    [ProducesResponseType<PageDto<RecapShareDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PageDto<RecapShareDto>>> ListRecapSharesAsync(
        [FromQuery] RecapShareScopeDto? scope = null,
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        this.ValidateEnum(scope, "scope");
        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        Guid userId = User.GetUserId();
        Page<RecapShareRow> page = await shares.ListAsync(
            userId,
            scope == RecapShareScopeDto.Own,
            offset,
            limit,
            cancellationToken);
        return Ok(page.ToDto(row => row.ToDto(userId)));
    }

    /// <summary>
    /// Shares a recap of the current profile with the other profiles. The days of the period are fixed now; the
    /// statistics are counted from the play history whenever the recap is opened. Sharing the same period (kind, days
    /// and time zone) again keeps the share and changes its design.
    /// </summary>
    /// <param name="request">The period and design of the recap.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="201">The new share; Location points to it.</response>
    /// <response code="200">The period was shared before; the share with its new design.</response>
    /// <response code="400">The request is invalid, e.g. from after to or an unknown time zone (code
    /// <c>request.invalid</c>).</response>
    /// <response code="404">The profile was deleted in the meantime (code <c>user.not_found</c>).</response>
    [HttpPost(Name = "CreateRecapShare")]
    [Consumes("application/json")]
    [ProducesResponseType<RecapShareDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<RecapShareDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<RecapShareDto>> CreateRecapShareAsync(
        CreateRecapShareDto request,
        CancellationToken cancellationToken)
    {
        this.ValidateEnum(request.Period, "period");
        if (request.From is { } first && !StatisticsLimits.IsInRange(first))
        {
            ModelState.AddValidationError("from", ValidationErrorCodeDto.InvalidValue);
        }

        if (request.To is { } last && !StatisticsLimits.IsInRange(last))
        {
            ModelState.AddValidationError("to", ValidationErrorCodeDto.InvalidValue);
        }

        if (request.From > request.To)
        {
            ModelState.AddValidationError("from", ValidationErrorCodeDto.InvalidValue);
        }

        if (!StatisticsTimeZones.TryFind(request.TimeZone, out _))
        {
            ModelState.AddValidationError("timeZone", ValidationErrorCodeDto.InvalidValue);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        Guid userId = User.GetUserId();
        RecapShareSaveResult result = await shares.ShareAsync(
            new RecapShareRequest(
                userId,
                request.Period!.Value.ToEntity(),
                request.From,
                request.To!.Value,
                request.TimeZone!,
                request.Design!),
            cancellationToken);
        RecapShareDto dto = result.Row.ToDto(userId);
        return result.Created ? CreatedAtRoute(GetShareRoute, new { id = dto.Id }, dto) : Ok(dto);
    }

    /// <summary>
    /// Returns a shared recap of any profile.
    /// </summary>
    /// <param name="id">Id of the share.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The share.</response>
    /// <response code="404">The share does not exist (code <c>recap.share_not_found</c>).</response>
    [HttpGet("{id:guid}", Name = GetShareRoute)]
    [ProducesResponseType<RecapShareDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<RecapShareDto>> GetRecapShareAsync(Guid id, CancellationToken cancellationToken)
    {
        RecapShareRow? row = await shares.GetAsync(id, cancellationToken);
        return row is null
            ? this.Problem(ProblemCodeDto.RecapShareNotFound, StatusCodes.Status404NotFound)
            : Ok(row.ToDto(User.GetUserId()));
    }

    /// <summary>
    /// Returns a shared recap with the statistics of the profile that shared it, counted now for the days of the
    /// share. Favorites and ratings of the tracks are those of the current profile.
    /// </summary>
    /// <param name="id">Id of the share.</param>
    /// <param name="top">Length of each ranking.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The share with its statistics.</response>
    /// <response code="400">Invalid query parameters (code <c>request.invalid</c>).</response>
    /// <response code="404">The share does not exist (code <c>recap.share_not_found</c>).</response>
    [HttpGet("{id:guid}/statistics", Name = "GetRecapShareStatistics")]
    [ProducesResponseType<SharedRecapDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<SharedRecapDto>> GetRecapShareStatisticsAsync(
        Guid id,
        [FromQuery, Range(1, StatisticsLimits.MaxTop)] int top = StatisticsLimits.DefaultTop,
        CancellationToken cancellationToken = default)
    {
        Guid userId = User.GetUserId();
        SharedRecap? recap = await shares.GetStatisticsAsync(id, userId, top, cancellationToken);
        return recap is null
            ? this.Problem(ProblemCodeDto.RecapShareNotFound, StatusCodes.Status404NotFound)
            : Ok(new SharedRecapDto { Share = recap.Share.ToDto(userId), Statistics = recap.Statistics.ToDto() });
    }

    /// <summary>
    /// Takes back a recap the current profile shared; the other profiles no longer see it.
    /// </summary>
    /// <param name="id">Id of the share.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The recap is no longer shared.</response>
    /// <response code="403">Another profile shared the recap (code <c>auth.forbidden</c>).</response>
    /// <response code="404">The share does not exist (code <c>recap.share_not_found</c>).</response>
    [HttpDelete("{id:guid}", Name = "DeleteRecapShare")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DeleteRecapShareAsync(Guid id, CancellationToken cancellationToken)
    {
        return await shares.DeleteAsync(User.GetUserId(), id, cancellationToken) switch
        {
            RecapShareDeletion.Deleted => NoContent(),
            RecapShareDeletion.NotOwner => this.Problem(ProblemCodeDto.AuthForbidden, StatusCodes.Status403Forbidden),
            _ => this.Problem(ProblemCodeDto.RecapShareNotFound, StatusCodes.Status404NotFound),
        };
    }
}
