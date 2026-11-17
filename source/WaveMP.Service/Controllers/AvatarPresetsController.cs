using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Caching.Covers;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Users.Avatars;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Queries;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The built-in avatars that profiles can choose instead of uploading a picture (PUT /api/v1/me/avatar with
/// <c>presetId</c>).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/avatar-presets")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class AvatarPresetsController(IAvatarService avatars) : ControllerBase
{
    /// <summary>
    /// Lists the built-in avatars in the order clients offer them.
    /// </summary>
    /// <param name="offset">Number of avatars to skip.</param>
    /// <param name="limit">Maximum number of avatars of the page.</param>
    /// <response code="200">One page of built-in avatars.</response>
    /// <response code="400">Invalid paging parameters (code <c>request.invalid</c>).</response>
    [HttpGet(Name = "ListAvatarPresets")]
    [ProducesResponseType<PageDto<AvatarPresetDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public ActionResult<PageDto<AvatarPresetDto>> ListAvatarPresets(
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit)
    {
        IReadOnlyList<string> ids = AvatarPresets.Ids;
        return Ok(new PageDto<AvatarPresetDto>
        {
            Items = [.. ids.Skip(offset).Take(limit).Select(id => new AvatarPresetDto { Id = id })],
            Offset = offset,
            Limit = limit,
            Total = ids.Count,
        });
    }

    /// <summary>
    /// Returns the picture of a built-in avatar as square WebP picture of 96, 300 or 1000 pixels (the requested size
    /// rounded up, at most 1000). Every picture carries a strong ETag and Cache-Control: private, no-cache; a request
    /// with a matching If-None-Match returns 304.
    /// </summary>
    /// <param name="id">Id of the built-in avatar.</param>
    /// <param name="size">Largest width and height in pixels the client shows the picture with (1 to 4096); the
    /// server rounds it up to 96, 300 or 1000. The largest picture if missing.</param>
    /// <response code="200">The picture.</response>
    /// <response code="400">Invalid size (code <c>request.invalid</c>).</response>
    /// <response code="404">There is no built-in avatar with this id (code <c>avatar.not_found</c>).</response>
    [HttpGet("{id}/picture", Name = "GetAvatarPresetPicture")]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, ImageFormats.WebP)]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult GetAvatarPresetPicture(
        string id,
        [FromQuery, Range(CoverSizes.MinRequested, CoverSizes.MaxRequested)] int? size = null)
    {
        CoverDeliveryResult result = avatars.GetPreset(id, size);
        return this.Cover(result, ProblemCodeDto.AvatarNotFound, ProblemCodeDto.AvatarNotFound);
    }
}
