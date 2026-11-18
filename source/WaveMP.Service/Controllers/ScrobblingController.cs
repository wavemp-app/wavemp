using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Scrobbling.Connections;
using WaveMP.Core.Scrobbling.Plays;
using WaveMP.Core.Subsonic.NowPlaying;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Controllers;

/// <summary>
/// Scrobbling of the profile of the current session: played songs are reported to Last.fm and ListenBrainz, each with
/// the account of the profile. Plays recorded in the play history (CreateMyHistoryEntry) are scrobbled to every
/// connected service; songs shorter than 30 seconds are not reported. Scrobbles wait in a queue on the server until the
/// service takes them, also across outages of the service and restarts of the server.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/me")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class ScrobblingController(
    IScrobbleConnectionService connections,
    INowPlayingService nowPlaying,
    ISubsonicNowPlayingRegistry nowPlayingRegistry) : ControllerBase
{
    /// <summary>
    /// Returns every scrobbling service with its state for the current profile.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The services, Last.fm first.</response>
    [HttpGet("scrobbling", Name = "GetMyScrobbling")]
    [ProducesResponseType<ScrobblingDto>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<ScrobblingDto>> GetMyScrobblingAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ScrobbleServiceState> states = await connections.GetStatesAsync(
            User.GetUserId(),
            cancellationToken);
        return Ok(states.ToDto());
    }

    /// <summary>
    /// Connects a scrobbling service to the current profile, or replaces its connection (e.g. after the service
    /// rejected the old one). Last.fm: the token Last.fm passed to the callback URL of the web authentication.
    /// ListenBrainz: the personal user token. The server checks the token with the service and stores the credentials
    /// encrypted; queued scrobbles are sent again from now on.
    /// </summary>
    /// <param name="service">The service.</param>
    /// <param name="request">The token.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The connected service.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>), e.g. an unknown service or no
    /// token.</response>
    /// <response code="404">The profile was deleted in the meantime (code <c>user.not_found</c>).</response>
    /// <response code="409">The server has no API account for the service (code
    /// <c>scrobbling.not_configured</c>).</response>
    /// <response code="422">The service did not accept the token (code <c>scrobbling.token_invalid</c>).</response>
    /// <response code="503">The service cannot be reached (code <c>scrobbling.service_unavailable</c>).</response>
    [HttpPut("scrobbling/{service}", Name = "ConnectMyScrobblingService")]
    [Consumes("application/json")]
    [ProducesResponseType<ScrobblingServiceDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ScrobblingServiceDto>> ConnectMyScrobblingServiceAsync(
        ScrobbleServiceDto service,
        ConnectScrobblingServiceDto request,
        CancellationToken cancellationToken)
    {
        this.ValidateEnum<ScrobbleServiceDto>(service, nameof(service));
        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        ScrobbleConnectResult result = await connections.ConnectAsync(
            User.GetUserId(),
            service.ToEntity(),
            request.Token,
            cancellationToken);
        return result.Status switch
        {
            ScrobbleConnectStatus.Connected when result.IsConnected => Ok(result.State.ToDto()),
            ScrobbleConnectStatus.NotConfigured => this.Problem(
                ProblemCodeDto.ScrobblingNotConfigured,
                StatusCodes.Status409Conflict),
            ScrobbleConnectStatus.InvalidToken => this.Problem(
                ProblemCodeDto.ScrobblingTokenInvalid,
                StatusCodes.Status422UnprocessableEntity),
            _ => this.Problem(ProblemCodeDto.ScrobblingServiceUnavailable, StatusCodes.Status503ServiceUnavailable),
        };
    }

    /// <summary>
    /// Disconnects a scrobbling service from the current profile. The stored credentials and the scrobbles that still
    /// wait for the service are deleted. The access of WaveMP can additionally be revoked in the settings of the
    /// service.
    /// </summary>
    /// <param name="service">The service.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The service is disconnected.</response>
    /// <response code="400">An unknown service (code <c>request.invalid</c>).</response>
    /// <response code="404">The service is not connected (code <c>scrobbling.not_connected</c>).</response>
    [HttpDelete("scrobbling/{service}", Name = "DisconnectMyScrobblingService")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DisconnectMyScrobblingServiceAsync(
        ScrobbleServiceDto service,
        CancellationToken cancellationToken)
    {
        this.ValidateEnum<ScrobbleServiceDto>(service, nameof(service));
        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        return await connections.DisconnectAsync(User.GetUserId(), service.ToEntity(), cancellationToken)
            ? NoContent()
            : this.Problem(ProblemCodeDto.ScrobblingNotConnected, StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Reports the track the current profile starts to listen to as "Now Playing" to every connected scrobbling
    /// service. Clients send it when the playback of a track starts. Tracks shorter than 30 seconds, tracks without
    /// artist and profiles without a connected service are accepted without a report; failures of a service are not
    /// reported to the client, since "Now Playing" is not repeated. The track also shows in "Now Playing" of the
    /// Subsonic API (getNowPlaying).
    /// </summary>
    /// <param name="request">The track.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">Accepted.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>).</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>).</response>
    [HttpPut("now-playing", Name = "UpdateMyNowPlaying")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> UpdateMyNowPlayingAsync(
        UpdateNowPlayingDto request,
        CancellationToken cancellationToken)
    {
        Guid userId = User.GetUserId();
        NowPlayingStatus status = await nowPlaying.ReportAsync(userId, request.TrackId, cancellationToken);
        if (status == NowPlayingStatus.TrackNotFound)
        {
            return this.Problem(ProblemCodeDto.TrackNotFound, StatusCodes.Status404NotFound);
        }

        nowPlayingRegistry.Report(userId, request.TrackId, playerName: null);
        return NoContent();
    }
}
