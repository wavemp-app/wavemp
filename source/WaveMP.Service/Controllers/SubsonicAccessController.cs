using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Subsonic.Accounts;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The Subsonic access of the profile of the current session. Subsonic apps (e.g. on phones) play the library through
/// the Subsonic API at <c>/rest</c> of the server: they sign in with the name of the profile and its app password.
/// The access is off by default; while it is off, every sign-in of the profile fails.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/me/subsonic")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class SubsonicAccessController(ISubsonicAccountService accounts) : ControllerBase
{
    private const string GetRoute = "GetMySubsonicAccess";

    /// <summary>
    /// Returns the Subsonic access of the current profile. The app password is never returned.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The Subsonic access.</response>
    [HttpGet(Name = GetRoute)]
    [ProducesResponseType<SubsonicAccessDto>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<SubsonicAccessDto>> GetMySubsonicAccessAsync(CancellationToken cancellationToken)
    {
        SubsonicAccountState state = await accounts.GetAsync(User.GetUserId(), cancellationToken);
        return Ok(state.ToDto());
    }

    /// <summary>
    /// Switches the Subsonic access of the current profile on or off. Missing or null values stay unchanged.
    /// </summary>
    /// <param name="request">The changes.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The changed Subsonic access.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>).</response>
    /// <response code="404">The profile was deleted in the meantime (code <c>user.not_found</c>).</response>
    [HttpPatch(Name = "UpdateMySubsonicAccess")]
    [Consumes("application/json")]
    [ProducesResponseType<SubsonicAccessDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<SubsonicAccessDto>> UpdateMySubsonicAccessAsync(
        UpdateSubsonicAccessDto request,
        CancellationToken cancellationToken)
    {
        Guid userId = User.GetUserId();
        SubsonicAccountState state = request.Enabled is { } enabled
            ? await accounts.SetEnabledAsync(userId, enabled, cancellationToken)
            : await accounts.GetAsync(userId, cancellationToken);
        return Ok(state.ToDto());
    }

    /// <summary>
    /// Creates a new app password for the current profile; it replaces the earlier one at once, so apps signed in with
    /// it must sign in again. The password is returned this once. It is also the OpenSubsonic API key.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="201">The new app password; Location points to the Subsonic access.</response>
    /// <response code="404">The profile was deleted in the meantime (code <c>user.not_found</c>).</response>
    [HttpPost("password", Name = "CreateMySubsonicPassword")]
    [ProducesResponseType<SubsonicPasswordDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<SubsonicPasswordDto>> CreateMySubsonicPasswordAsync(
        CancellationToken cancellationToken)
    {
        SubsonicPassword password = await accounts.CreatePasswordAsync(User.GetUserId(), cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return CreatedAtRoute(GetRoute, null, password.ToDto());
    }

    /// <summary>
    /// Revokes the app password of the current profile; Subsonic apps can no longer sign in until a new one is
    /// created. Without a password, nothing changes.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The app password is revoked.</response>
    [HttpDelete("password", Name = "DeleteMySubsonicPassword")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMySubsonicPasswordAsync(CancellationToken cancellationToken)
    {
        await accounts.RevokePasswordAsync(User.GetUserId(), cancellationToken);
        return NoContent();
    }
}
