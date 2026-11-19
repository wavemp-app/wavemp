using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Playlists.SmartPlaylists;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;
using WaveMP.Service.Queries;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The built-in templates of smart playlists, the same for every profile.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/smart-playlist-templates")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class SmartPlaylistTemplatesController : ControllerBase
{
    /// <summary>
    /// Lists the built-in templates in the order clients offer them: rules, sort order and limit a new smart playlist
    /// can start with (CreateMySmartPlaylist with a name of the client).
    /// </summary>
    /// <param name="offset">Number of templates to skip.</param>
    /// <param name="limit">Maximum number of templates of the page.</param>
    /// <response code="200">One page of templates.</response>
    /// <response code="400">Invalid paging parameters (code <c>request.invalid</c>).</response>
    [HttpGet(Name = "ListSmartPlaylistTemplates")]
    [ProducesResponseType<PageDto<SmartPlaylistTemplateDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public ActionResult<PageDto<SmartPlaylistTemplateDto>> ListSmartPlaylistTemplates(
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit)
    {
        IReadOnlyList<SmartPlaylistTemplate> templates = SmartPlaylistTemplates.All;
        return Ok(new PageDto<SmartPlaylistTemplateDto>
        {
            Items = [.. templates.Skip(offset).Take(limit).Select(template => template.ToDto())],
            Offset = offset,
            Limit = limit,
            Total = templates.Count,
        });
    }
}
