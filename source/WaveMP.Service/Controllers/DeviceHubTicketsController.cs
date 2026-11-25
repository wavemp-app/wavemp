using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// Tickets of the real-time connection of the devices (asyncapi.yaml) for clients that cannot send a header when they
/// open it, like browsers.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/me/device-hub-tickets")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class DeviceHubTicketsController(ITokenIssuer tokenIssuer) : ControllerBase
{
    /// <summary>
    /// Issues a ticket that opens the real-time connection of the devices for the current session, valid for 60
    /// seconds. Clients pass it in the query parameter <c>access_token</c> (never the session token) and fetch a new
    /// one for every connection attempt. The ticket is accepted by nothing but the connection of the devices; the
    /// connection stays open while the session is active. Clients that can send headers may open the connection with
    /// the session token in the header Authorization instead.
    /// </summary>
    /// <response code="200">The ticket.</response>
    [HttpPost(Name = "CreateDeviceHubTicket")]
    [ProducesResponseType<DeviceHubTicketDto>(StatusCodes.Status200OK, "application/json")]
    public ActionResult<DeviceHubTicketDto> CreateDeviceHubTicket()
    {
        DeviceHubTicket ticket = tokenIssuer.IssueDeviceHubTicket(User.GetUserId(), User.GetSessionId());
        Response.Headers.CacheControl = "no-store";
        return Ok(new DeviceHubTicketDto { Ticket = ticket.Ticket, ExpiresAt = ticket.ExpiresAt });
    }
}
