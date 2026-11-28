using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Analysis.Services;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// Where the real song lies in the files of a track, for admins only: what the song analysis found in the primary
/// audio file and the primary video (without intro, outro and added parts), the corrections of the admin and a new
/// analysis on request.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class TrackSongSegmentsController(ISongSegmentService segments) : ControllerBase
{
    private const string GetTrackSongSegmentsRoute = "GetTrackSongSegments";

    /// <summary>
    /// Returns the song segments of the primary audio file and the primary video of a track: what the analysis found,
    /// the corrections of the admin and the segment the players use.
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The song segments.</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>).</response>
    [HttpGet("tracks/{id:guid}/song-segments", Name = GetTrackSongSegmentsRoute)]
    [ProducesResponseType<TrackSongSegmentsDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TrackSongSegmentsDto>> GetTrackSongSegmentsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        TrackSongSegments? result = await segments.GetAsync(id, cancellationToken);
        return result is null
            ? this.Problem(ProblemCodeDto.TrackNotFound, StatusCodes.Status404NotFound)
            : Ok(result.ToDto());
    }

    /// <summary>
    /// Corrects where the song starts and ends in the primary audio file or the primary video of a track, in
    /// milliseconds of the file. The correction is stored in the database (files are never written), stays across
    /// scans and is never changed by the analysis; a value left out comes from the analysis or the file.
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="kind">The file: Audio or Video.</param>
    /// <param name="request">Start and end of the song.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The song segments after the correction.</response>
    /// <response code="400">Start or end outside the file, or the end not after the start (code
    /// <c>request.invalid</c>, field <c>startMilliseconds</c> or <c>endMilliseconds</c> with <c>out_of_range</c>
    /// and min and max).</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>) or has no file of the kind (code
    /// <c>track.no_audio</c> or <c>track.no_video</c>).</response>
    [HttpPut("tracks/{id:guid}/song-segments/{kind}", Name = "SetTrackSongSegment")]
    [Consumes("application/json")]
    [ProducesResponseType<TrackSongSegmentsDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TrackSongSegmentsDto>> SetTrackSongSegmentAsync(
        Guid id,
        SongSegmentKindDto kind,
        UpdateTrackSongSegmentDto request,
        CancellationToken cancellationToken)
    {
        this.ValidateEnum<SongSegmentKindDto>(kind, nameof(kind));
        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        SongSegmentUpdate result = await segments.SetAsync(
            id,
            kind.ToEntity(),
            request.StartMilliseconds,
            request.EndMilliseconds,
            cancellationToken);
        return ToResult(kind, result);
    }

    /// <summary>
    /// Removes the correction of the primary audio file or the primary video of a track, so the players use what the
    /// analysis found again.
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="kind">The file: Audio or Video.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The song segments after the change.</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>) or has no file of the kind (code
    /// <c>track.no_audio</c> or <c>track.no_video</c>).</response>
    [HttpDelete("tracks/{id:guid}/song-segments/{kind}", Name = "ResetTrackSongSegment")]
    [ProducesResponseType<TrackSongSegmentsDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TrackSongSegmentsDto>> ResetTrackSongSegmentAsync(
        Guid id,
        SongSegmentKindDto kind,
        CancellationToken cancellationToken)
    {
        this.ValidateEnum<SongSegmentKindDto>(kind, nameof(kind));
        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        SongSegmentUpdate result = await segments.SetAsync(id, kind.ToEntity(), null, null, cancellationToken);
        return ToResult(kind, result);
    }

    /// <summary>
    /// Lets the song analysis analyze the files of a track again in the background (job <c>song-analysis</c>), e.g.
    /// after its lyrics changed. Corrections of the admin stay.
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="202">The analysis is queued; Location points to the song segments of the track, whose files
    /// show as pending until they are analyzed.</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>).</response>
    [HttpPost("tracks/{id:guid}/song-analyses", Name = "AnalyzeTrackSong")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> AnalyzeTrackSongAsync(Guid id, CancellationToken cancellationToken)
    {
        return await segments.AnalyzeAgainAsync(id, cancellationToken)
            ? AcceptedAtRoute(GetTrackSongSegmentsRoute, new { id }, null)
            : this.Problem(ProblemCodeDto.TrackNotFound, StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Lets the song analysis analyze all files of the library again in the background (job <c>song-analysis</c>,
    /// at a low priority). Corrections of the admin stay.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="202">The analysis is queued; Location points to the job <c>song-analysis</c> and its state.
    /// </response>
    [HttpPost("song-analyses", Name = "AnalyzeAllSongs")]
    [ProducesResponseType<SongAnalysesStartedDto>(StatusCodes.Status202Accepted, "application/json")]
    public async Task<ActionResult<SongAnalysesStartedDto>> AnalyzeAllSongsAsync(CancellationToken cancellationToken)
    {
        int renewed = await segments.AnalyzeAllAgainAsync(cancellationToken);
        return AcceptedAtRoute(
            JobsController.GetJobRoute,
            new { id = JobIds.SongAnalysis },
            new SongAnalysesStartedDto { Renewed = renewed });
    }

    private ActionResult<TrackSongSegmentsDto> ToResult(SongSegmentKindDto kind, SongSegmentUpdate result)
    {
        switch (result.Status)
        {
            case SongSegmentUpdateStatus.Saved:
                return Ok(result.Segments!.ToDto());
            case SongSegmentUpdateStatus.Invalid:
                ModelState.AddValidationError(
                    result.InvalidField!,
                    ValidationErrorCodeDto.OutOfRange,
                    result.Minimum,
                    result.Maximum);
                return ValidationProblem();
            case SongSegmentUpdateStatus.NoFile:
                return this.Problem(
                    kind == SongSegmentKindDto.Audio ? ProblemCodeDto.TrackNoAudio : ProblemCodeDto.TrackNoVideo,
                    StatusCodes.Status404NotFound);
            default:
                return this.Problem(ProblemCodeDto.TrackNotFound, StatusCodes.Status404NotFound);
        }
    }
}
