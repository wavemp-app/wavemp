using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Scanning.Selections;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Queries;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// Overview of the file selection of all tracks, for admins only: which tracks have more than one file to choose from
/// and which use a file the admin selected.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/file-selections")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class FileSelectionsController(IFileSelectionService selections) : ControllerBase
{
    /// <summary>
    /// Lists the tracks with their number of audio files, video files and covers and the state of their file
    /// selection per category, optionally filtered and searched.
    /// </summary>
    /// <param name="offset">Number of tracks to skip.</param>
    /// <param name="limit">Maximum number of tracks of the page.</param>
    /// <param name="sort">Sort key; Path by default.</param>
    /// <param name="direction">Sort direction; ascending by default, descending for FileCount.</param>
    /// <param name="filter">Which tracks to list; All by default.</param>
    /// <param name="q">Only tracks whose title, folder or file name contains the text, without regard to case.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">One page of tracks.</response>
    /// <response code="400">Invalid query parameters (code <c>request.invalid</c>).</response>
    [HttpGet(Name = "ListFileSelections")]
    [ProducesResponseType<PageDto<TrackFileSummaryDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PageDto<TrackFileSummaryDto>>> ListFileSelectionsAsync(
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit,
        [FromQuery] FileSelectionSortDto? sort = null,
        [FromQuery] SortDirectionDto? direction = null,
        [FromQuery] FileSelectionFilterDto? filter = null,
        [FromQuery, StringLength(LibraryLimits.TextMaxLength)] string? q = null,
        CancellationToken cancellationToken = default)
    {
        this.ValidateEnum(sort, nameof(sort));
        this.ValidateEnum(direction, nameof(direction));
        this.ValidateEnum(filter, nameof(filter));
        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        Page<TrackFileSummaryRow> page = await selections.ListAsync(
            new FileSelectionListRequest(
                offset,
                limit,
                sort?.ToEntity(),
                direction?.ToEntity(),
                filter?.ToEntity(),
                q),
            cancellationToken);
        return Ok(page.ToDto(row => row.ToDto()));
    }
}
