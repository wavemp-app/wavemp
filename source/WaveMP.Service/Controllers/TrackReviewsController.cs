using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Analysis.Services;
using WaveMP.Core.Library.Reviews;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Queries;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The review inbox, for admins only: whether an admin checked the metadata of a track. Every track starts
/// unreviewed, also the tracks found by later scans; scans and the metadata editor never change the status.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class TrackReviewsController(ITrackReviewService reviews, ISongSegmentService segments) : ControllerBase
{
    /// <summary>
    /// Lists the tracks of a review status (unreviewed by default) with the fields their tags lack, sorted, filtered
    /// and searched.
    /// </summary>
    /// <param name="offset">Number of tracks to skip.</param>
    /// <param name="limit">Maximum number of tracks of the page.</param>
    /// <param name="sort">Sort key; FirstSeenAt by default.</param>
    /// <param name="direction">Sort direction; descending (the newest first) by default for FirstSeenAt, otherwise
    /// ascending.</param>
    /// <param name="status">Review status of the tracks; Unreviewed by default.</param>
    /// <param name="missingTags">Only tracks whose tags lack a checked field (true) or have all of them (false).
    /// </param>
    /// <param name="q">Only tracks whose title, artist, album, folder or file name contains the text, without regard
    /// to case.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">One page of tracks.</response>
    /// <response code="400">Invalid query parameters (code <c>request.invalid</c>).</response>
    [HttpGet("track-reviews", Name = "ListTrackReviews")]
    [ProducesResponseType<PageDto<TrackReviewDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PageDto<TrackReviewDto>>> ListTrackReviewsAsync(
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit,
        [FromQuery] TrackReviewSortDto? sort = null,
        [FromQuery] SortDirectionDto? direction = null,
        [FromQuery] TrackReviewStatusDto? status = null,
        [FromQuery] bool? missingTags = null,
        [FromQuery, StringLength(LibraryLimits.TextMaxLength)] string? q = null,
        CancellationToken cancellationToken = default)
    {
        this.ValidateEnum(sort, nameof(sort));
        this.ValidateEnum(direction, nameof(direction));
        this.ValidateEnum(status, nameof(status));
        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        Page<TrackReviewRow> page = await reviews.ListAsync(
            new TrackReviewListRequest(
                offset,
                limit,
                sort?.ToEntity(),
                direction?.ToEntity(),
                status?.ToEntity(),
                missingTags,
                q),
            cancellationToken);
        IReadOnlyDictionary<Guid, SongSegmentNotice> notices = await segments.FindNoticesAsync(
            [.. page.Items.Select(row => row.TrackId)],
            cancellationToken);
        return Ok(page.ToDto(row => row.ToDto(notices.TryGetValue(row.TrackId, out SongSegmentNotice notice)
            ? notice
            : null)));
    }

    /// <summary>
    /// Counts the tracks per review status, for example for the badge of the review inbox.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The number of unreviewed and reviewed tracks.</response>
    [HttpGet("track-reviews/summary", Name = "GetTrackReviewSummary")]
    [ProducesResponseType<TrackReviewSummaryDto>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<TrackReviewSummaryDto>> GetTrackReviewSummaryAsync(
        CancellationToken cancellationToken)
    {
        return Ok((await reviews.CountAsync(cancellationToken)).ToDto());
    }

    /// <summary>
    /// Sets the review status of several tracks at once, for example marks the selected tracks of the inbox as
    /// reviewed by the current profile. Tracks that have the status already keep it unchanged (a reviewed track keeps
    /// its time and profile); unknown tracks and duplicates are skipped.
    /// </summary>
    /// <param name="request">The tracks and their new status.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The number of tracks whose status changed.</response>
    /// <response code="400">The tracks or the status are missing, or there are more than 500 tracks (code
    /// <c>request.invalid</c>).</response>
    [HttpPost("track-reviews/batch", Name = "UpdateTrackReviews")]
    [Consumes("application/json")]
    [ProducesResponseType<UpdatedTrackReviewsDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<UpdatedTrackReviewsDto>> UpdateTrackReviewsAsync(
        UpdateTrackReviewsDto request,
        CancellationToken cancellationToken)
    {
        int changed = await reviews.SetManyAsync(
            request.TrackIds,
            request.Status!.Value.ToEntity(),
            User.GetUserId(),
            cancellationToken);
        return Ok(new UpdatedTrackReviewsDto { ChangedCount = changed });
    }

    /// <summary>
    /// Returns the review status of a track.
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The review status of the track.</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>).</response>
    [HttpGet("tracks/{id:guid}/review", Name = "GetTrackReview")]
    [ProducesResponseType<TrackReviewStateDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TrackReviewStateDto>> GetTrackReviewAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        TrackReviewStateRow? state = await reviews.GetAsync(id, cancellationToken);
        return state is null
            ? this.Problem(ProblemCodeDto.TrackNotFound, StatusCodes.Status404NotFound)
            : Ok(state.ToDto());
    }

    /// <summary>
    /// Sets the review status of a track: Reviewed marks it as reviewed by the current profile now, Unreviewed puts it
    /// back into the review inbox. A track that has the status already keeps it unchanged (a reviewed track keeps its
    /// time and profile).
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="request">The new status.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The review status of the track.</response>
    /// <response code="400">The status is missing (code <c>request.invalid</c>).</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>).</response>
    [HttpPut("tracks/{id:guid}/review", Name = "SetTrackReview")]
    [Consumes("application/json")]
    [ProducesResponseType<TrackReviewStateDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TrackReviewStateDto>> SetTrackReviewAsync(
        Guid id,
        UpdateTrackReviewDto request,
        CancellationToken cancellationToken)
    {
        TrackReviewStateRow? state = await reviews.SetAsync(
            id,
            request.Status!.Value.ToEntity(),
            User.GetUserId(),
            cancellationToken);
        return state is null
            ? this.Problem(ProblemCodeDto.TrackNotFound, StatusCodes.Status404NotFound)
            : Ok(state.ToDto());
    }
}
