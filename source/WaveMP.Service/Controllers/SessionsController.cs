using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Users.Sessions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Hubs;
using WaveMP.Service.Mappings;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// Sessions: selecting a profile creates a session with a token, switching the profile ends it.
/// </summary>
[ApiController]
[Route("api/v1/sessions")]
public sealed class SessionsController(
    ISessionService sessions,
    ITokenIssuer tokenIssuer,
    DeviceHubSessionCloser deviceHub,
    TimeProvider timeProvider) : ControllerBase
{
    private const string CurrentSessionRoute = "GetCurrentSession";

    /// <summary>
    /// Selects a profile: creates a session and returns its token. A profile with <c>hasPin</c> needs its PIN. Every
    /// given PIN counts as an attempt; after several wrong PINs in a row, the profile is locked out for a while.
    /// </summary>
    /// <param name="request">The profile to select and its PIN.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="201">The session with its token; Location points to the current session.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>), e.g. a PIN that is not 4 to 8
    /// digits (<c>invalid_format</c>).</response>
    /// <response code="401">The profile needs a PIN and the request has none (code <c>profile.pin_required</c>), or
    /// the PIN is wrong (code <c>profile.pin_invalid</c>).</response>
    /// <response code="404">The profile does not exist (code <c>profile.not_found</c>).</response>
    /// <response code="429">Too many wrong PINs: the profile is locked out (code <c>profile.pin_locked</c>); the
    /// header Retry-After says after how many seconds the next attempt is possible.</response>
    [HttpPost(Name = "CreateSession")]
    [AllowAnonymous]
    [Consumes("application/json")]
    [ProducesResponseType<CreatedSessionDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests, "application/problem+json")]
    public async Task<ActionResult<CreatedSessionDto>> CreateSessionAsync(
        CreateSessionDto request,
        CancellationToken cancellationToken)
    {
        SessionResult result = await sessions.CreateAsync(request.ProfileId, request.Pin, cancellationToken);
        if (!result.IsSuccess)
        {
            return Failure(result);
        }

        ActiveSession session = result.Session;
        CreatedSessionDto created = new() { Token = tokenIssuer.Issue(session), Session = session.ToDto() };
        return CreatedAtRoute(CurrentSessionRoute, null, created);
    }

    /// <summary>
    /// Returns the session of the token, with its profile.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The current session.</response>
    /// <response code="401">No valid token, or the session has ended (code <c>auth.unauthorized</c>).</response>
    [HttpGet("current", Name = CurrentSessionRoute)]
    [Authorize]
    [ProducesResponseType<SessionDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<SessionDto>> GetCurrentSessionAsync(CancellationToken cancellationToken)
    {
        ActiveSession? session = await sessions.GetActiveAsync(User.GetSessionId(), cancellationToken);
        if (session is null)
        {
            // The session ended after the token was checked.
            return this.Problem(ProblemCodeDto.AuthUnauthorized, StatusCodes.Status401Unauthorized);
        }

        return Ok(session.ToDto());
    }

    /// <summary>
    /// Ends the session of the token (switching the profile); the token is no longer accepted and the real-time
    /// connections of the devices opened with it are closed.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The session has ended.</response>
    /// <response code="401">No valid token, or the session has ended (code <c>auth.unauthorized</c>).</response>
    [HttpDelete("current", Name = "DeleteCurrentSession")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<IActionResult> DeleteCurrentSessionAsync(CancellationToken cancellationToken)
    {
        Guid sessionId = User.GetSessionId();
        await sessions.DeleteAsync(sessionId, cancellationToken);
        await deviceHub.CloseSessionAsync(sessionId, cancellationToken);
        return NoContent();
    }

    private ObjectResult Failure(SessionResult result)
    {
        switch (result.Error)
        {
            case SessionError.ProfileNotFound:
                return this.Problem(ProblemCodeDto.ProfileNotFound, StatusCodes.Status404NotFound);
            case SessionError.PinRequired:
                return this.Problem(ProblemCodeDto.ProfilePinRequired, StatusCodes.Status401Unauthorized);
            case SessionError.PinInvalid:
                return this.Problem(ProblemCodeDto.ProfilePinInvalid, StatusCodes.Status401Unauthorized);
            case SessionError.PinLockedOut:
                Response.Headers.RetryAfter = RetryAfterSeconds(result.LockoutEnd)
                    .ToString(CultureInfo.InvariantCulture);
                return this.Problem(ProblemCodeDto.ProfilePinLocked, StatusCodes.Status429TooManyRequests);
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Error, "Not a failure.");
        }
    }

    /// <summary>
    /// Whole seconds until the lockout ends, at least one.
    /// </summary>
    private long RetryAfterSeconds(DateTimeOffset? lockoutEnd)
    {
        TimeSpan remaining = (lockoutEnd ?? DateTimeOffset.MinValue) - timeProvider.GetUtcNow();
        return Math.Max(1, (long)Math.Ceiling(remaining.TotalSeconds));
    }
}
