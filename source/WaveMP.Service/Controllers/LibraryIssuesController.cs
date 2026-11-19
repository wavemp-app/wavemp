using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Library.Checks;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Queries;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The problems of the library, for admins only: tracks without cover or tags, with an unknown album, possible
/// duplicates, unreadable files and, as hints, videos without audio and songs without video. The job library-check
/// finds them after every scan; it is started by hand through StartJobRun with the id <c>library-check</c>.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/library/issues")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class LibraryIssuesController(ILibraryIssueService issues) : ControllerBase
{
    /// <summary>
    /// Most kinds of problem accepted in one request; every kind once is fewer.
    /// </summary>
    internal const int MaxTypes = 20;

    /// <summary>
    /// Lists the problems the last run of the library check found, sorted, filtered and searched.
    /// </summary>
    /// <param name="offset">Number of problems to skip.</param>
    /// <param name="limit">Maximum number of problems of the page.</param>
    /// <param name="sort">Sort key; Path by default.</param>
    /// <param name="direction">Sort direction; descending (the newest first) by default for DetectedAt, otherwise
    /// ascending.</param>
    /// <param name="type">Only problems of these kinds (repeated query parameter, at most 20); all kinds without it.
    /// </param>
    /// <param name="severity">Only problems of this severity; together with type only the kinds of both.</param>
    /// <param name="q">Only problems whose track title, artist, album, folder or file name, or whose file path,
    /// contains the text, without regard to case.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">One page of problems.</response>
    /// <response code="400">Invalid query parameters (code <c>request.invalid</c>).</response>
    [HttpGet(Name = "ListLibraryIssues")]
    [ProducesResponseType<PageDto<LibraryIssueDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PageDto<LibraryIssueDto>>> ListLibraryIssuesAsync(
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit,
        [FromQuery] LibraryIssueSortDto? sort = null,
        [FromQuery] SortDirectionDto? direction = null,
        [FromQuery] LibraryIssueTypeDto[]? type = null,
        [FromQuery] LibraryIssueSeverityDto? severity = null,
        [FromQuery, StringLength(LibraryLimits.TextMaxLength)] string? q = null,
        CancellationToken cancellationToken = default)
    {
        this.ValidateEnum(sort, nameof(sort));
        this.ValidateEnum(direction, nameof(direction));
        this.ValidateEnum(severity, nameof(severity));
        ValidateTypes(type);
        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        LibraryIssueType[]? types = type is { Length: > 0 } ? [.. type.Select(value => value.ToEntity())] : null;
        Page<LibraryIssueRow> page = await issues.ListAsync(
            new LibraryIssueListRequest(
                offset,
                limit,
                sort?.ToEntity(),
                direction?.ToEntity(),
                types,
                severity?.ToEntity(),
                q),
            cancellationToken);
        return Ok(page.ToDto(row => row.ToDto()));
    }

    /// <summary>
    /// Returns the number of problems per kind (also kinds without problems) and when the library check ran last, for
    /// the overview of the administration.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The overview of the library check.</response>
    [HttpGet("summary", Name = "GetLibraryIssueSummary")]
    [ProducesResponseType<LibraryIssueSummaryDto>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<LibraryIssueSummaryDto>> GetLibraryIssueSummaryAsync(
        CancellationToken cancellationToken)
    {
        return Ok((await issues.GetSummaryAsync(cancellationToken)).ToDto());
    }

    /// <summary>
    /// Model binding of query parameters also accepts numbers for enums, including undefined ones.
    /// </summary>
    private void ValidateTypes(LibraryIssueTypeDto[]? types)
    {
        const string Field = "type";
        if (types is null)
        {
            return;
        }

        if (types.Length > MaxTypes)
        {
            ModelState.AddValidationError(Field, ValidationErrorCodeDto.MaxItems, max: MaxTypes);
            return;
        }

        for (int index = 0; index < types.Length; index++)
        {
            if (!Enum.IsDefined(types[index]))
            {
                ModelState.AddValidationError($"{Field}[{index}]", ValidationErrorCodeDto.InvalidValue);
            }
        }
    }
}
