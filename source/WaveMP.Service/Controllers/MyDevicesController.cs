using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Devices.Devices;
using WaveMP.Data.Queries;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Queries;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The devices of the current profile: every open instance of a client registers as a device over the real-time
/// connection of the devices (asyncapi.yaml). Devices can be renamed, and closed devices forgotten.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/me/devices")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class MyDevicesController(IDeviceService devices) : ControllerBase
{
    // Route constraint of a device id; other ids answer 404 like an unknown device.
    private const string DeviceIdRoute = "{deviceId:regex(^[[A-Za-z0-9._:-]]{{1,64}}$)}";

    /// <summary>
    /// Lists the devices of the current profile: open devices first, then the most recently active. A profile keeps
    /// at most 50 devices; opening another one forgets the least recently active closed devices above the limit.
    /// </summary>
    /// <param name="offset">Number of devices to skip.</param>
    /// <param name="limit">Maximum number of devices of the page.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">One page of devices.</response>
    /// <response code="400">Invalid query parameters (code <c>request.invalid</c>).</response>
    [HttpGet(Name = "ListMyDevices")]
    [ProducesResponseType<PageDto<DeviceDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PageDto<DeviceDto>>> ListMyDevicesAsync(
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        Page<DeviceView> page = await devices.ListAsync(User.GetUserId(), offset, limit, cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return Ok(page.ToDto(device => device.ToDto()));
    }

    /// <summary>
    /// Renames a device of the current profile. The open devices of the profile learn the new name at once.
    /// </summary>
    /// <param name="deviceId">Id of the device.</param>
    /// <param name="request">The new name.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The device with its name.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>): a <c>name</c> that is empty after
    /// removing the spaces at its ends (<c>required</c>), longer than 100 characters (<c>max_length</c>) or with
    /// control characters (<c>invalid_format</c>).</response>
    /// <response code="404">The current profile has no device with this id (code <c>device.not_found</c>).</response>
    [HttpPatch(DeviceIdRoute, Name = "UpdateMyDevice")]
    [Consumes("application/json")]
    [ProducesResponseType<DeviceDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<DeviceDto>> UpdateMyDeviceAsync(
        string deviceId,
        UpdateDeviceDto request,
        CancellationToken cancellationToken)
    {
        Guid userId = User.GetUserId();
        if (request.Name is null)
        {
            DeviceView? device = await devices.GetAsync(userId, deviceId, cancellationToken);
            return device is null ? NotFoundProblem() : Ok(device.ToDto());
        }

        DeviceRenameResult result = await devices.RenameAsync(userId, deviceId, request.Name, cancellationToken);
        switch (result.Status)
        {
            case DeviceRenameStatus.Renamed:
                return Ok(result.Device!.ToDto());
            case DeviceRenameStatus.NotFound:
                return NotFoundProblem();
            case DeviceRenameStatus.Invalid:
                ModelState.AddValidationError(
                    "name",
                    result.Error!.Code.ToDto(),
                    (int?)result.Error.Min,
                    (int?)result.Error.Max);
                return ValidationProblem();
            default:
                throw new InvalidOperationException($"Unknown status {result.Status}.");
        }
    }

    /// <summary>
    /// Forgets a closed device of the current profile. It appears again when it opens WaveMP again.
    /// </summary>
    /// <param name="deviceId">Id of the device.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The device is forgotten.</response>
    /// <response code="404">The current profile has no device with this id (code <c>device.not_found</c>).</response>
    /// <response code="409">The device is open right now (code <c>device.online</c>).</response>
    [HttpDelete(DeviceIdRoute, Name = "DeleteMyDevice")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> DeleteMyDeviceAsync(string deviceId, CancellationToken cancellationToken)
    {
        DeviceDeleteStatus status = await devices.DeleteAsync(User.GetUserId(), deviceId, cancellationToken);
        return status switch
        {
            DeviceDeleteStatus.Deleted => NoContent(),
            DeviceDeleteStatus.NotFound => NotFoundProblem(),
            DeviceDeleteStatus.Online => this.Problem(ProblemCodeDto.DeviceOnline, StatusCodes.Status409Conflict),
            _ => throw new InvalidOperationException($"Unknown status {status}."),
        };
    }

    private ObjectResult NotFoundProblem()
    {
        return this.Problem(ProblemCodeDto.DeviceNotFound, StatusCodes.Status404NotFound);
    }
}
