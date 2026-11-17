using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WaveMP.Core.Users.Avatars;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Filters;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The avatar of the profile of the current session. Its picture comes from GET /api/v1/users/{id}/avatar.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/me/avatar")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class MyAvatarController(IAvatarService avatars, IOptions<AvatarOptions> options) : ControllerBase
{
    /// <summary>
    /// Sets the avatar of the current profile: an uploaded JPEG, PNG or WebP picture in <c>file</c> (10 MiB at most by
    /// default) or a built-in avatar in <c>presetId</c>, never both. The server cuts the largest square out of the
    /// middle of an uploaded picture, stores it as WebP in several sizes and discards the uploaded file. Replaces an
    /// earlier avatar.
    /// </summary>
    /// <param name="request">The picture or the built-in avatar.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The profile with its new avatar.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>): neither or both of <c>file</c> and
    /// <c>presetId</c> (<c>required</c>, <c>invalid_value</c>) or an unknown built-in avatar
    /// (<c>presetId</c>: <c>invalid_value</c>).</response>
    /// <response code="404">The profile was deleted in the meantime (code <c>user.not_found</c>).</response>
    /// <response code="413">The picture is too large (code <c>avatar.too_large</c>).</response>
    /// <response code="415">The picture is not a JPEG, PNG or WebP (code <c>avatar.unsupported_format</c>).</response>
    /// <response code="422">The picture is damaged (code <c>avatar.unreadable</c>).</response>
    /// <response code="503">Pictures cannot be processed, ffmpeg is missing (code
    /// <c>service.unavailable</c>).</response>
    [HttpPut(Name = "SetMyAvatar")]
    [Consumes("multipart/form-data")]
    [AvatarUploadLimit]
    [ProducesResponseType<ProfileDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public Task<ActionResult> SetMyAvatarAsync([FromForm] SetAvatarDto request, CancellationToken cancellationToken)
    {
        return this.SetAvatarAsync(
            avatars,
            options.Value.MaxUploadBytes,
            User.GetUserId(),
            request,
            user => user.ToProfileDto(),
            cancellationToken);
    }

    /// <summary>
    /// Removes the avatar of the current profile; it shows its initials again. Succeeds also without an avatar.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The profile has no avatar.</response>
    /// <response code="404">The profile was deleted in the meantime (code <c>user.not_found</c>).</response>
    [HttpDelete(Name = "DeleteMyAvatar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DeleteMyAvatarAsync(CancellationToken cancellationToken)
    {
        return await avatars.RemoveAsync(User.GetUserId(), cancellationToken)
            ? NoContent()
            : this.Problem(ProblemCodeDto.UserNotFound, StatusCodes.Status404NotFound);
    }
}
