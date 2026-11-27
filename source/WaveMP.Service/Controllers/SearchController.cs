using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Library.Searches;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Queries;

namespace WaveMP.Service.Controllers;

/// <summary>
/// Search over the whole library.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/search")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class SearchController(ISearchService search) : ControllerBase
{
    /// <summary>
    /// Searches artists, albums and tracks at once and returns the best matches of each, sorted by relevance. The
    /// search is tolerant: it finds names that contain the search text (without regard to case, accents and
    /// punctuation), names with symbols that stand for letters in both directions ("pink" finds "P!nk", "kesha"
    /// "Ke$ha"; "!" as i, "$" as s, "@" as a, "0" as o, "3" as e, "&amp;" and "+" as "and"), artists by their aliases
    /// (results show the name of the artist) and names similar to it, so typos are fine ("rihana" finds "Rihanna").
    /// Exact matches come first, then names that start with the text, names that contain it, artists found by an
    /// alias and similar names. Albums are also found by their artists, tracks by their artists and album. The lists
    /// of artists, albums and tracks with q and the Subsonic search use the same search. To see more of a group, use
    /// the parameter q of its list.
    /// </summary>
    /// <param name="q">Search text, 1 to 200 characters; white space only is invalid.</param>
    /// <param name="offset">Number of matches of each group to skip.</param>
    /// <param name="limit">Maximum number of matches of each group.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The matches.</response>
    /// <response code="400">Invalid query parameters (code <c>request.invalid</c>).</response>
    [HttpGet(Name = "SearchLibrary")]
    [ProducesResponseType<SearchResultDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<SearchResultDto>> SearchLibraryAsync(
        [FromQuery, Required, StringLength(LibraryLimits.TextMaxLength, MinimumLength = 1)] string q,
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.SearchMaxLimit)] int limit = PageLimits.SearchDefaultLimit,
        CancellationToken cancellationToken = default)
    {
        SearchResult? result = await search.SearchAsync(q, offset, limit, User.GetUserId(), cancellationToken);
        if (result is null)
        {
            // White space only, or nothing left after normalization.
            ModelState.AddValidationError(nameof(q), ValidationErrorCodeDto.Required);
            return ValidationProblem();
        }

        return Ok(result.ToDto());
    }
}
