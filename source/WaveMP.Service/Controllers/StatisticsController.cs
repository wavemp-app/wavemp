using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Recaps.Statistics;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Queries;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The statistics of the play history of the current profile, the basis of its recaps.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/me/stats")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class StatisticsController(IListeningStatisticsService statistics) : ControllerBase
{
    /// <summary>
    /// Counts the plays of the current profile in a period: the most played tracks, artists, albums and genres, the
    /// listening time, the number of songs, the plays over time and per hour and the artists played for the first
    /// time. Every play counts with the whole duration of its track; plays of tracks that left the library are gone.
    /// </summary>
    /// <param name="from">First day of the period (YYYY-MM-DD, from 1900-01-01); without it, the period starts with
    /// the first play.</param>
    /// <param name="to">Last day of the period (up to 2999-12-31); without it, today in the time zone.</param>
    /// <param name="timeZone">IANA time zone the days and hours are counted in, e.g. Europe/Berlin; UTC without
    /// it.</param>
    /// <param name="top">Length of each ranking.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The statistics.</response>
    /// <response code="400">Invalid query parameters, e.g. from after to or an unknown time zone (code
    /// <c>request.invalid</c>).</response>
    [HttpGet(Name = "GetMyStatistics")]
    [ProducesResponseType<ListeningStatisticsDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<ListeningStatisticsDto>> GetMyStatisticsAsync(
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery, StringLength(StatisticsTimeZones.MaxLength)] string? timeZone = null,
        [FromQuery, Range(1, StatisticsLimits.MaxTop)] int top = StatisticsLimits.DefaultTop,
        CancellationToken cancellationToken = default)
    {
        if (from is { } first && !StatisticsLimits.IsInRange(first))
        {
            ModelState.AddValidationError("from", ValidationErrorCodeDto.InvalidValue);
        }

        if (to is { } last && !StatisticsLimits.IsInRange(last))
        {
            ModelState.AddValidationError("to", ValidationErrorCodeDto.InvalidValue);
        }

        if (from > to)
        {
            ModelState.AddValidationError("from", ValidationErrorCodeDto.InvalidValue);
        }

        if (!StatisticsTimeZones.TryFind(timeZone ?? StatisticsTimeZones.Default, out TimeZoneInfo zone))
        {
            ModelState.AddValidationError("timeZone", ValidationErrorCodeDto.InvalidValue);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        Guid userId = User.GetUserId();
        ListeningStatistics result = await statistics.GetAsync(
            new StatisticsQuery(userId, from, to, zone, top, userId),
            cancellationToken);
        return Ok(result.ToDto());
    }
}
