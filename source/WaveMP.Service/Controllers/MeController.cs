using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Users.Pins;
using WaveMP.Core.Users.Settings;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Controllers;

/// <summary>
/// Data of the profile of the current session.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/me")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class MeController(IUserSettingsService settings, IPinService pins) : ControllerBase
{
    /// <summary>
    /// Returns the settings of the current profile.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The settings.</response>
    /// <response code="404">The profile was deleted in the meantime (code <c>user.not_found</c>).</response>
    [HttpGet("settings", Name = "GetMySettings")]
    [ProducesResponseType<UserSettingsDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<UserSettingsDto>> GetMySettingsAsync(CancellationToken cancellationToken)
    {
        UserSettings? result = await settings.GetAsync(User.GetUserId(), cancellationToken);
        return result is null
            ? this.Problem(ProblemCodeDto.UserNotFound, StatusCodes.Status404NotFound)
            : Ok(result.ToDto());
    }

    /// <summary>
    /// Changes the settings of the current profile. Missing or null values stay unchanged.
    /// </summary>
    /// <param name="request">The changes.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The changed settings.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>), e.g. <c>equalizer.preampDb</c> or an
    /// entry of <c>equalizer.customGainsDb</c> out of range (<c>out_of_range</c>), not exactly 10 values in
    /// <c>equalizer.customGainsDb</c> (<c>min_items</c>, <c>max_items</c>), <c>crossfade.seconds</c> or
    /// <c>sleepTimer.fadeSeconds</c> out of range, or an unknown preset (<c>invalid_format</c>).</response>
    /// <response code="404">The profile was deleted in the meantime (code <c>user.not_found</c>).</response>
    [HttpPatch("settings", Name = "UpdateMySettings")]
    [Consumes("application/json")]
    [ProducesResponseType<UserSettingsDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<UserSettingsDto>> UpdateMySettingsAsync(
        UpdateUserSettingsDto request,
        CancellationToken cancellationToken)
    {
        UserSettings? result = await settings.UpdateAsync(User.GetUserId(), request.ToCommand(), cancellationToken);
        return result is null
            ? this.Problem(ProblemCodeDto.UserNotFound, StatusCodes.Status404NotFound)
            : Ok(result.ToDto());
    }

    /// <summary>
    /// Returns whether the current profile is protected by a PIN and whether the PIN comes from ADMIN_PIN.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The state of the PIN.</response>
    /// <response code="404">The profile was deleted in the meantime (code <c>user.not_found</c>).</response>
    [HttpGet("pin", Name = "GetMyPin")]
    [ProducesResponseType<PinStatusDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PinStatusDto>> GetMyPinAsync(CancellationToken cancellationToken)
    {
        PinStatus? status = await pins.GetStatusAsync(User.GetUserId(), cancellationToken);
        return status is null
            ? this.Problem(ProblemCodeDto.UserNotFound, StatusCodes.Status404NotFound)
            : Ok(status.ToDto());
    }

    /// <summary>
    /// Sets or changes the PIN of the current profile. All other sessions of the profile end, so other devices have
    /// to enter the new PIN; the current session stays.
    /// </summary>
    /// <param name="request">The new PIN.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The new state of the PIN.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>), e.g. a PIN that is not 4 to 8
    /// digits (<c>invalid_format</c>).</response>
    /// <response code="404">The profile was deleted in the meantime (code <c>user.not_found</c>).</response>
    /// <response code="409">The PIN is set by ADMIN_PIN (code <c>pin.managed_by_environment</c>).</response>
    [HttpPut("pin", Name = "SetMyPin")]
    [Consumes("application/json")]
    [ProducesResponseType<PinStatusDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<PinStatusDto>> SetMyPinAsync(SetPinDto request, CancellationToken cancellationToken)
    {
        PinChangeStatus status = await pins.SetAsync(
            User.GetUserId(),
            request.Pin,
            User.GetSessionId(),
            cancellationToken);
        return status == PinChangeStatus.Success
            ? Ok(new PinStatusDto { IsSet = true, IsManagedByEnvironment = false })
            : this.PinProblem(status);
    }

    /// <summary>
    /// Removes the PIN of the current profile; it can then be selected without a PIN.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The profile has no PIN (also if it had none before).</response>
    /// <response code="404">The profile was deleted in the meantime (code <c>user.not_found</c>).</response>
    /// <response code="409">The PIN is set by ADMIN_PIN (code <c>pin.managed_by_environment</c>).</response>
    [HttpDelete("pin", Name = "DeleteMyPin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> DeleteMyPinAsync(CancellationToken cancellationToken)
    {
        PinChangeStatus status = await pins.RemoveAsync(User.GetUserId(), cancellationToken);
        return status == PinChangeStatus.Success ? NoContent() : this.PinProblem(status);
    }
}
