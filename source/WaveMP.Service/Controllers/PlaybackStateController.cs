using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Controllers;

/// <summary>
/// Where the current profile stopped listening. The players save the queue, its current entry and position while
/// they play, so the profile continues after a reload or on another device; the most recently changed state wins.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/me/playback-state")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class PlaybackStateController(IPlaybackStateService states) : ControllerBase
{
    /// <summary>
    /// Returns the saved playback state of the current profile with the songs of its queue. Songs that left the
    /// library are left out; if the current song is gone, the next entry is current at its beginning. Clients load the
    /// state on start and show it without starting playback; the device tells whether another device saved it.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The saved state.</response>
    /// <response code="404">The profile has no saved state yet (code <c>playback.state_not_found</c>).</response>
    [HttpGet(Name = "GetMyPlaybackState")]
    [ProducesResponseType<PlaybackStateDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PlaybackStateDto>> GetMyPlaybackStateAsync(CancellationToken cancellationToken)
    {
        PlaybackStateView? state = await states.GetAsync(User.GetUserId(), cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return state is null
            ? this.Problem(ProblemCodeDto.PlaybackStateNotFound, StatusCodes.Status404NotFound)
            : Ok(state.ToDto());
    }

    /// <summary>
    /// Saves the playback state of a device for the current profile and replaces the saved one, unless that changed
    /// later (<c>changedAt</c>): the most recently changed state wins. Clients save on a change of song, on pause and
    /// every few seconds while playing, not on every progress update. Unknown songs are saved as well and left out
    /// when the state is read.
    /// </summary>
    /// <param name="request">The state of the device.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The state is saved.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>), e.g. more than 2000 <c>items</c>
    /// (<c>max_items</c>), a <c>currentIndex</c> outside the queue or a negative <c>positionMilliseconds</c>
    /// (<c>out_of_range</c>), an <c>originalOrder</c> of a shuffled queue that does not name every position once
    /// (<c>invalid_value</c>) or a missing <c>device</c> (<c>required</c>).</response>
    /// <response code="404">The profile was deleted in the meantime (code <c>user.not_found</c>).</response>
    /// <response code="409">Another device saved a state that changed later; it stays (code
    /// <c>playback.state_outdated</c>).</response>
    [HttpPut(Name = "SaveMyPlaybackState")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> SaveMyPlaybackStateAsync(
        UpdatePlaybackStateDto request,
        CancellationToken cancellationToken)
    {
        PlaybackStateSaveStatus status = await states.SaveAsync(
            User.GetUserId(),
            request.ToCommand(),
            cancellationToken);
        switch (status)
        {
            case PlaybackStateSaveStatus.Saved:
                return NoContent();
            case PlaybackStateSaveStatus.Outdated:
                return this.Problem(ProblemCodeDto.PlaybackStateOutdated, StatusCodes.Status409Conflict);
            case PlaybackStateSaveStatus.InvalidCurrentIndex:
                ModelState.AddValidationError(
                    "currentIndex",
                    ValidationErrorCodeDto.OutOfRange,
                    -1,
                    request.Items.Count - 1);
                return ValidationProblem();
            case PlaybackStateSaveStatus.InvalidOriginalOrder:
                ModelState.AddValidationError("originalOrder", ValidationErrorCodeDto.InvalidValue);
                return ValidationProblem();
            default:
                throw new InvalidOperationException($"Unknown status {status}.");
        }
    }

    /// <summary>
    /// Clears the saved playback state of the current profile, so the next start of a client begins without a queue
    /// (<c>GET</c> then answers 404). Succeeds also without a saved state. A device that still plays saves its state
    /// again; clients stop their own player before they clear the state.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The profile has no saved state.</response>
    [HttpDelete(Name = "ClearMyPlaybackState")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ClearMyPlaybackStateAsync(CancellationToken cancellationToken)
    {
        await states.ClearAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }
}
