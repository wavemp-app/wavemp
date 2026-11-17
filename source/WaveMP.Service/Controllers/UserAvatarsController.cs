using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WaveMP.Core.Caching.Covers;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Users.Avatars;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Filters;
using WaveMP.Service.Mappings;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The avatars of the profiles. The pictures are available without a session, like the profile selection; admins set
/// and remove the avatars of all profiles.
/// </summary>
[ApiController]
[Route("api/v1/users/{id:guid}/avatar")]
public sealed class UserAvatarsController(IAvatarService avatars, IOptions<AvatarOptions> options) : ControllerBase
{
    /// <summary>
    /// Returns the avatar of a profile as square WebP picture of 96, 300 or 1000 pixels (the requested size rounded
    /// up, at most 1000; an uploaded picture is never enlarged). Every picture carries a strong ETag of its version and
    /// size and Cache-Control: private, no-cache; a request with a matching If-None-Match returns 304. Available without
    /// a session, like the profile selection.
    /// </summary>
    /// <param name="id">Id of the profile.</param>
    /// <param name="size">Largest width and height in pixels the client shows the picture with (1 to 4096); the
    /// server rounds it up to 96, 300 or 1000. The largest picture if missing.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The picture.</response>
    /// <response code="400">Invalid size (code <c>request.invalid</c>).</response>
    /// <response code="404">The profile does not exist (code <c>user.not_found</c>) or has no avatar (code
    /// <c>avatar.not_found</c>); clients then show its initials.</response>
    [HttpGet(Name = "GetUserAvatar")]
    [AllowAnonymous]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, ImageFormats.WebP)]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetUserAvatarAsync(
        Guid id,
        [FromQuery, Range(CoverSizes.MinRequested, CoverSizes.MaxRequested)] int? size = null,
        CancellationToken cancellationToken = default)
    {
        CoverDeliveryResult result = await avatars.GetAsync(id, size, cancellationToken);
        return this.Cover(result, ProblemCodeDto.UserNotFound, ProblemCodeDto.AvatarNotFound);
    }

    /// <summary>
    /// Sets the avatar of a profile, for admins only: an uploaded JPEG, PNG or WebP picture in <c>file</c> (10 MiB at
    /// most by default) or a built-in avatar in <c>presetId</c>, never both. The server cuts the largest square out of
    /// the middle of an uploaded picture, stores it as WebP in several sizes and discards the uploaded file. Replaces
    /// an earlier avatar.
    /// </summary>
    /// <param name="id">Id of the profile.</param>
    /// <param name="request">The picture or the built-in avatar.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The profile with its new avatar.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>): neither or both of <c>file</c> and
    /// <c>presetId</c> (<c>required</c>, <c>invalid_value</c>) or an unknown built-in avatar
    /// (<c>presetId</c>: <c>invalid_value</c>).</response>
    /// <response code="401">No valid session.</response>
    /// <response code="403">The profile of the session is not an admin.</response>
    /// <response code="404">The profile does not exist (code <c>user.not_found</c>).</response>
    /// <response code="413">The picture is too large (code <c>avatar.too_large</c>).</response>
    /// <response code="415">The picture is not a JPEG, PNG or WebP (code <c>avatar.unsupported_format</c>).</response>
    /// <response code="422">The picture is damaged (code <c>avatar.unreadable</c>).</response>
    /// <response code="503">Pictures cannot be processed, ffmpeg is missing (code
    /// <c>service.unavailable</c>).</response>
    [HttpPut(Name = "SetUserAvatar")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [Consumes("multipart/form-data")]
    [AvatarUploadLimit]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public Task<ActionResult> SetUserAvatarAsync(
        Guid id,
        [FromForm] SetAvatarDto request,
        CancellationToken cancellationToken)
    {
        return this.SetAvatarAsync(
            avatars,
            options.Value.MaxUploadBytes,
            id,
            request,
            user => user.ToUserDto(),
            cancellationToken);
    }

    /// <summary>
    /// Removes the avatar of a profile, for admins only; it shows its initials again. Succeeds also without an avatar.
    /// </summary>
    /// <param name="id">Id of the profile.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The profile has no avatar.</response>
    /// <response code="401">No valid session.</response>
    /// <response code="403">The profile of the session is not an admin.</response>
    /// <response code="404">The profile does not exist (code <c>user.not_found</c>).</response>
    [HttpDelete(Name = "DeleteUserAvatar")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DeleteUserAvatarAsync(Guid id, CancellationToken cancellationToken)
    {
        return await avatars.RemoveAsync(id, cancellationToken)
            ? NoContent()
            : this.Problem(ProblemCodeDto.UserNotFound, StatusCodes.Status404NotFound);
    }
}
