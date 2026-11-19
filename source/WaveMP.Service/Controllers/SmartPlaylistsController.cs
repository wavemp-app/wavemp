using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Playlists.Names;
using WaveMP.Core.Playlists.SmartPlaylists;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Queries;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The smart playlists of the profile of the current session: rules that pick songs from the library, a sort order and
/// an optional limit. Their songs are computed on every request from the current library and the ratings, favorites and
/// plays of the profile. Every profile only sees its own smart playlists.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/me/smart-playlists")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class SmartPlaylistsController(ISmartPlaylistService smartPlaylists) : ControllerBase
{
    private const string GetSmartPlaylistRoute = "GetMySmartPlaylist";

    /// <summary>
    /// Lists the smart playlists of the current profile with their number of songs right now, sorted and optionally
    /// searched.
    /// </summary>
    /// <param name="offset">Number of smart playlists to skip.</param>
    /// <param name="limit">Maximum number of smart playlists of the page.</param>
    /// <param name="sort">Sort key; Name by default.</param>
    /// <param name="direction">Sort direction; ascending by default.</param>
    /// <param name="q">Search text: only smart playlists whose name contains it, without regard to case.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">One page of smart playlists.</response>
    /// <response code="400">Invalid query parameters (code <c>request.invalid</c>).</response>
    [HttpGet(Name = "ListMySmartPlaylists")]
    [ProducesResponseType<PageDto<SmartPlaylistDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PageDto<SmartPlaylistDto>>> ListMySmartPlaylistsAsync(
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit,
        [FromQuery] PlaylistSortDto sort = PlaylistSortDto.Name,
        [FromQuery] SortDirectionDto direction = SortDirectionDto.Asc,
        [FromQuery, StringLength(PlaylistNames.MaxLength)] string? q = null,
        CancellationToken cancellationToken = default)
    {
        this.ValidateEnum<PlaylistSortDto>(sort, nameof(sort));
        this.ValidateEnum<SortDirectionDto>(direction, nameof(direction));
        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        PlaylistListQuery query = new(
            User.GetUserId(),
            offset,
            limit,
            sort.ToEntity(),
            direction.ToEntity(),
            string.IsNullOrWhiteSpace(q) ? null : q.Trim());
        Page<SmartPlaylistItem> page = await smartPlaylists.ListAsync(query, cancellationToken);
        return Ok(page.ToDto(item => item.ToDto()));
    }

    /// <summary>
    /// Returns a smart playlist of the current profile with its number of songs right now.
    /// </summary>
    /// <param name="id">Id of the smart playlist.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The smart playlist.</response>
    /// <response code="404">The smart playlist does not exist or belongs to another profile (code
    /// <c>playlist.smart_not_found</c>).</response>
    [HttpGet("{id:guid}", Name = GetSmartPlaylistRoute)]
    [ProducesResponseType<SmartPlaylistDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<SmartPlaylistDto>> GetMySmartPlaylistAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        SmartPlaylistItem? item = await smartPlaylists.GetAsync(User.GetUserId(), id, cancellationToken);
        return item is null ? NotFoundProblem() : Ok(item.ToDto());
    }

    /// <summary>
    /// Creates a smart playlist of the current profile, e.g. from a template of ListSmartPlaylistTemplates.
    /// </summary>
    /// <param name="request">Name, rules, sort order and limit.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="201">The new smart playlist; Location points to it.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>), e.g. a name of spaces only
    /// (<c>name</c>, <c>required</c>), more than 20 rules (<c>rules</c>, <c>max_items</c>), an operator that does not
    /// fit its field (<c>rules[i].operator</c>, <c>invalid_value</c>), a missing value (<c>rules[i].text</c> or
    /// <c>rules[i].number</c>, <c>required</c>) or a number outside the range of its field (<c>out_of_range</c> with
    /// min and max).</response>
    /// <response code="404">The profile was deleted in the meantime (code <c>user.not_found</c>).</response>
    [HttpPost(Name = "CreateMySmartPlaylist")]
    [Consumes("application/json")]
    [ProducesResponseType<SmartPlaylistDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<SmartPlaylistDto>> CreateMySmartPlaylistAsync(
        SaveSmartPlaylistDto request,
        CancellationToken cancellationToken)
    {
        SmartPlaylistResult result = await smartPlaylists.CreateAsync(
            User.GetUserId(),
            request.ToDefinition(),
            cancellationToken);
        return result.IsOk
            ? CreatedAtRoute(GetSmartPlaylistRoute, new { id = result.Item.Playlist.Id }, result.Item.ToDto())
            : Failure(result);
    }

    /// <summary>
    /// Replaces name, rules, sort order and limit of a smart playlist of the current profile.
    /// </summary>
    /// <param name="id">Id of the smart playlist.</param>
    /// <param name="request">Name, rules, sort order and limit.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The changed smart playlist.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>), as for
    /// CreateMySmartPlaylist.</response>
    /// <response code="404">The smart playlist does not exist or belongs to another profile (code
    /// <c>playlist.smart_not_found</c>).</response>
    [HttpPut("{id:guid}", Name = "UpdateMySmartPlaylist")]
    [Consumes("application/json")]
    [ProducesResponseType<SmartPlaylistDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<SmartPlaylistDto>> UpdateMySmartPlaylistAsync(
        Guid id,
        SaveSmartPlaylistDto request,
        CancellationToken cancellationToken)
    {
        SmartPlaylistResult result = await smartPlaylists.UpdateAsync(
            User.GetUserId(),
            id,
            request.ToDefinition(),
            cancellationToken);
        return result.IsOk ? Ok(result.Item.ToDto()) : Failure(result);
    }

    /// <summary>
    /// Deletes a smart playlist of the current profile. The songs stay in the library.
    /// </summary>
    /// <param name="id">Id of the smart playlist.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The smart playlist is deleted.</response>
    /// <response code="404">The smart playlist does not exist or belongs to another profile (code
    /// <c>playlist.smart_not_found</c>).</response>
    [HttpDelete("{id:guid}", Name = "DeleteMySmartPlaylist")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DeleteMySmartPlaylistAsync(Guid id, CancellationToken cancellationToken)
    {
        return await smartPlaylists.DeleteAsync(User.GetUserId(), id, cancellationToken)
            ? NoContent()
            : NotFoundProblem();
    }

    /// <summary>
    /// Lists the songs of a smart playlist in its order, computed now from the current library.
    /// </summary>
    /// <param name="id">Id of the smart playlist.</param>
    /// <param name="offset">Number of songs to skip.</param>
    /// <param name="limit">Maximum number of songs of the page.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">One page of songs.</response>
    /// <response code="400">Invalid query parameters (code <c>request.invalid</c>).</response>
    /// <response code="404">The smart playlist does not exist or belongs to another profile (code
    /// <c>playlist.smart_not_found</c>).</response>
    [HttpGet("{id:guid}/tracks", Name = "ListMySmartPlaylistTracks")]
    [ProducesResponseType<PageDto<TrackDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PageDto<TrackDto>>> ListMySmartPlaylistTracksAsync(
        Guid id,
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        Page<TrackRow>? page = await smartPlaylists.ListTracksAsync(
            User.GetUserId(),
            id,
            offset,
            limit,
            cancellationToken);
        return page is null ? NotFoundProblem() : Ok(page.ToDto(track => track.ToDto()));
    }

    /// <summary>
    /// Converts a smart playlist into a normal playlist with the same name and its songs of right now, in their order.
    /// The smart playlist is deleted; the new playlist no longer follows the rules.
    /// </summary>
    /// <param name="id">Id of the smart playlist.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="201">The new playlist; Location points to it.</response>
    /// <response code="404">The smart playlist does not exist or belongs to another profile (code
    /// <c>playlist.smart_not_found</c>).</response>
    /// <response code="409">The smart playlist holds more songs than a playlist may have (code
    /// <c>playlist.full</c>).</response>
    /// <response code="503">The library kept changing during the conversion, e.g. during a scan (code
    /// <c>service.unavailable</c>); nothing was changed.</response>
    [HttpPost("{id:guid}/conversions", Name = "ConvertMySmartPlaylist")]
    [ProducesResponseType<PlaylistDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<PlaylistDto>> ConvertMySmartPlaylistAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        SmartPlaylistConversion conversion = await smartPlaylists.ConvertAsync(
            User.GetUserId(),
            id,
            cancellationToken);
        if (conversion.IsOk)
        {
            return CreatedAtRoute(
                "GetMyPlaylist",
                new { id = conversion.Playlist.Playlist.Id },
                conversion.Playlist.ToDto());
        }

        return conversion.Status switch
        {
            SmartPlaylistStatus.NotFound => NotFoundProblem(),
            SmartPlaylistStatus.Full => this.Problem(ProblemCodeDto.PlaylistFull, StatusCodes.Status409Conflict),
            _ => this.Problem(ProblemCodeDto.ServiceUnavailable, StatusCodes.Status503ServiceUnavailable),
        };
    }

    private ObjectResult NotFoundProblem()
    {
        return this.Problem(ProblemCodeDto.SmartPlaylistNotFound, StatusCodes.Status404NotFound);
    }

    private ActionResult Failure(SmartPlaylistResult result)
    {
        if (result.Status != SmartPlaylistStatus.Invalid)
        {
            return NotFoundProblem();
        }

        foreach (SmartPlaylistError error in result.Errors)
        {
            ModelState.AddValidationError(error.Field, error.Code.ToDto(), error.Min, error.Max);
        }

        return ValidationProblem();
    }
}
