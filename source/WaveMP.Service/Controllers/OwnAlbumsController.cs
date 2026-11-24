using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Organizing.Options;
using WaveMP.Core.Organizing.OwnAlbums;
using WaveMP.Data.Queries;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.OpenApiTransformers;
using WaveMP.Service.Queries;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// Own albums, for admins only: albums put together from songs of the library. Changing an own album touches no
/// file; an organize preview of kind OwnAlbum shows what applying it would change.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/own-albums")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class OwnAlbumsController(IOwnAlbumService ownAlbums, IOptions<OrganizeOptions> options)
    : ControllerBase
{
    private const string GetOwnAlbumRoute = "GetOwnAlbum";
    private const int BufferSize = 81_920;

    /// <summary>
    /// Lists the own albums.
    /// </summary>
    /// <param name="offset">Number of albums to skip.</param>
    /// <param name="limit">Maximum number of albums of the page.</param>
    /// <param name="sort">Sort key; Title by default.</param>
    /// <param name="direction">Sort direction; ascending by default.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">One page of own albums.</response>
    /// <response code="400">Invalid query parameters (code <c>request.invalid</c>).</response>
    [HttpGet(Name = "ListOwnAlbums")]
    [ProducesResponseType<PageDto<OwnAlbumDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PageDto<OwnAlbumDto>>> ListOwnAlbumsAsync(
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit,
        [FromQuery] OwnAlbumSortDto sort = OwnAlbumSortDto.Title,
        [FromQuery] SortDirectionDto direction = SortDirectionDto.Asc,
        CancellationToken cancellationToken = default)
    {
        this.ValidateEnum<OwnAlbumSortDto>(sort, nameof(sort));
        this.ValidateEnum<SortDirectionDto>(direction, nameof(direction));
        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        Page<OwnAlbumItem> page = await ownAlbums.ListAsync(
            sort.ToQuery(direction, offset, limit),
            cancellationToken);
        return Ok(page.ToDto(item => item.ToDto()));
    }

    /// <summary>
    /// Returns an own album.
    /// </summary>
    /// <param name="id">Id of the own album.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The own album.</response>
    /// <response code="404">The own album does not exist (code <c>organize.own_album_not_found</c>).</response>
    [HttpGet("{id:guid}", Name = GetOwnAlbumRoute)]
    [ProducesResponseType<OwnAlbumDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OwnAlbumDto>> GetOwnAlbumAsync(Guid id, CancellationToken cancellationToken)
    {
        OwnAlbumItem? item = await ownAlbums.GetAsync(id, cancellationToken);
        return item is null ? NotFoundProblem() : Ok(item.ToDto());
    }

    /// <summary>
    /// Creates an own album without songs.
    /// </summary>
    /// <param name="request">The album.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="201">The new own album; Location points to it.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>).</response>
    [HttpPost(Name = "CreateOwnAlbum")]
    [Consumes("application/json")]
    [ProducesResponseType<OwnAlbumDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<OwnAlbumDto>> CreateOwnAlbumAsync(
        CreateOwnAlbumDto request,
        CancellationToken cancellationToken)
    {
        OwnAlbumResult result = await ownAlbums.CreateAsync(request.ToValues(), User.GetUserId(), cancellationToken);
        return result is { IsOk: true, Item: { } item }
            ? CreatedAtRoute(GetOwnAlbumRoute, new { id = item.Album.Id }, item.ToDto())
            : Failure(result);
    }

    /// <summary>
    /// Changes title, album artist, year or genre of an own album. No file changes until the album is applied again.
    /// </summary>
    /// <param name="id">Id of the own album.</param>
    /// <param name="request">The changes.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The changed own album.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>).</response>
    /// <response code="404">The own album does not exist (code <c>organize.own_album_not_found</c>).</response>
    [HttpPatch("{id:guid}", Name = "UpdateOwnAlbum")]
    [Consumes("application/json")]
    [ProducesResponseType<OwnAlbumDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OwnAlbumDto>> UpdateOwnAlbumAsync(
        Guid id,
        UpdateOwnAlbumDto request,
        CancellationToken cancellationToken)
    {
        OwnAlbumResult result = await ownAlbums.UpdateAsync(id, request.ToChanges(), cancellationToken);
        return result is { IsOk: true, Item: { } item } ? Ok(item.ToDto()) : Failure(result);
    }

    /// <summary>
    /// Deletes an own album and its cover. The songs and their files stay as they are; the log of organize operations
    /// keeps its entries.
    /// </summary>
    /// <param name="id">Id of the own album.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The own album was deleted.</response>
    /// <response code="404">The own album does not exist (code <c>organize.own_album_not_found</c>).</response>
    [HttpDelete("{id:guid}", Name = "DeleteOwnAlbum")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DeleteOwnAlbumAsync(Guid id, CancellationToken cancellationToken)
    {
        return await ownAlbums.DeleteAsync(id, cancellationToken) ? NoContent() : NotFoundProblem();
    }

    /// <summary>
    /// Lists the songs of an own album by position, then the songs taken out since the last apply (state Removed).
    /// </summary>
    /// <param name="id">Id of the own album.</param>
    /// <param name="offset">Number of songs to skip.</param>
    /// <param name="limit">Maximum number of songs of the page.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">One page of songs.</response>
    /// <response code="400">Invalid query parameters (code <c>request.invalid</c>).</response>
    /// <response code="404">The own album does not exist (code <c>organize.own_album_not_found</c>).</response>
    [HttpGet("{id:guid}/tracks", Name = "ListOwnAlbumTracks")]
    [ProducesResponseType<PageDto<OwnAlbumTrackDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PageDto<OwnAlbumTrackDto>>> ListOwnAlbumTracksAsync(
        Guid id,
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        Page<OwnAlbumTrackItem>? page = await ownAlbums.ListTracksAsync(id, offset, limit, cancellationToken);
        return page is null ? NotFoundProblem() : Ok(page.ToDto(item => item.ToDto()));
    }

    /// <summary>
    /// Sets the songs of an own album in this order (also to sort them). Songs left out are dropped, or marked as
    /// Removed if the album was applied, so the next apply restores their original tags.
    /// </summary>
    /// <param name="id">Id of the own album.</param>
    /// <param name="request">The songs.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The changed own album.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>).</response>
    /// <response code="404">The own album (code <c>organize.own_album_not_found</c>) or a song (code <c>track.not_found</c>)
    /// does not exist.</response>
    /// <response code="409">A song has no audio file (code <c>track.no_audio</c>), belongs to another own album (code
    /// <c>organize.track_in_other_own_album</c>), or the album would get too many songs (code
    /// <c>organize.own_album_too_many_tracks</c>).</response>
    [HttpPut("{id:guid}/tracks", Name = "SetOwnAlbumTracks")]
    [Consumes("application/json")]
    [ProducesResponseType<OwnAlbumDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OwnAlbumDto>> SetOwnAlbumTracksAsync(
        Guid id,
        OwnAlbumTracksDto request,
        CancellationToken cancellationToken)
    {
        OwnAlbumResult result = await ownAlbums.ReplaceTracksAsync(id, request.TrackIds, cancellationToken);
        return result is { IsOk: true, Item: { } item } ? Ok(item.ToDto()) : Failure(result);
    }

    /// <summary>
    /// Adds songs at the end of an own album; songs that are members already stay where they are.
    /// </summary>
    /// <param name="id">Id of the own album.</param>
    /// <param name="request">The songs.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The changed own album.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>).</response>
    /// <response code="404">The own album (code <c>organize.own_album_not_found</c>) or a song (code <c>track.not_found</c>)
    /// does not exist.</response>
    /// <response code="409">A song has no audio file (code <c>track.no_audio</c>), belongs to another own album (code
    /// <c>organize.track_in_other_own_album</c>), or the album would get too many songs (code
    /// <c>organize.own_album_too_many_tracks</c>).</response>
    [HttpPost("{id:guid}/tracks", Name = "AddOwnAlbumTracks")]
    [Consumes("application/json")]
    [ProducesResponseType<OwnAlbumDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OwnAlbumDto>> AddOwnAlbumTracksAsync(
        Guid id,
        OwnAlbumTracksDto request,
        CancellationToken cancellationToken)
    {
        OwnAlbumResult result = await ownAlbums.AddTracksAsync(id, request.TrackIds, cancellationToken);
        return result is { IsOk: true, Item: { } item } ? Ok(item.ToDto()) : Failure(result);
    }

    /// <summary>
    /// Takes a song out of an own album: dropped, or marked as Removed if the album was applied.
    /// </summary>
    /// <param name="id">Id of the own album.</param>
    /// <param name="trackId">Id of the song.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The changed own album.</response>
    /// <response code="404">The own album (code <c>organize.own_album_not_found</c>) does not exist or the song is not part of
    /// it (code <c>track.not_found</c>).</response>
    [HttpDelete("{id:guid}/tracks/{trackId:guid}", Name = "RemoveOwnAlbumTrack")]
    [ProducesResponseType<OwnAlbumDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OwnAlbumDto>> RemoveOwnAlbumTrackAsync(
        Guid id,
        Guid trackId,
        CancellationToken cancellationToken)
    {
        OwnAlbumResult result = await ownAlbums.RemoveTrackAsync(id, trackId, cancellationToken);
        return result is { IsOk: true, Item: { } item } ? Ok(item.ToDto()) : Failure(result);
    }

    /// <summary>
    /// Returns the cover of an own album as uploaded, with a strong ETag, Last-Modified and Cache-Control: private,
    /// no-cache; a request with a matching If-None-Match returns 304.
    /// </summary>
    /// <param name="id">Id of the own album.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The picture.</response>
    /// <response code="404">The own album does not exist (code <c>organize.own_album_not_found</c>) or has no cover (code
    /// <c>cover.not_found</c>).</response>
    [HttpGet("{id:guid}/cover", Name = "GetOwnAlbumCover")]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, ImageFormats.Jpeg, ImageFormats.Png, ImageFormats.WebP)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetOwnAlbumCoverAsync(Guid id, CancellationToken cancellationToken)
    {
        (bool albumExists, OwnAlbumCover? cover) = await ownAlbums.OpenCoverAsync(id, cancellationToken);
        if (cover is null)
        {
            return albumExists
                ? this.Problem(ProblemCodeDto.CoverNotFound, StatusCodes.Status404NotFound)
                : NotFoundProblem();
        }

        Response.Headers.CacheControl = ControllerBaseExtensions.CoverCacheControl;
        return File(
            cover.Content,
            cover.ContentType,
            cover.LastModified,
            new EntityTagHeaderValue($"\"{cover.ETag}\""));
    }

    /// <summary>
    /// Uploads the cover of an own album as the raw bytes of a JPEG, PNG or WebP picture (10 MiB at most by default).
    /// The picture is kept as uploaded; applying the album writes it as cover.{ext} into the album folder. Replaces an
    /// earlier cover.
    /// </summary>
    /// <param name="id">Id of the own album.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The own album with its new cover.</response>
    /// <response code="404">The own album does not exist (code <c>organize.own_album_not_found</c>).</response>
    /// <response code="413">The picture is too large (code <c>cover.too_large</c>).</response>
    /// <response code="415">The content is not a JPEG, PNG or WebP picture (code
    /// <c>cover.unsupported_format</c>).</response>
    /// <response code="503">The cover cannot be stored (code <c>service.unavailable</c>).</response>
    [HttpPut("{id:guid}/cover", Name = "SetOwnAlbumCover")]
    [BinaryRequestBody(ImageFormats.Jpeg, ImageFormats.Png, ImageFormats.WebP)]
    [ProducesResponseType<OwnAlbumDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<OwnAlbumDto>> SetOwnAlbumCoverAsync(Guid id, CancellationToken cancellationToken)
    {
        long maxBytes = options.Value.MaxCoverBytes;
        if (Request.ContentLength > maxBytes)
        {
            return this.Problem(ProblemCodeDto.CoverTooLarge, StatusCodes.Status413PayloadTooLarge);
        }

        await using MemoryStream? content = await ReadBodyAsync(maxBytes, cancellationToken);
        if (content is null)
        {
            return this.Problem(ProblemCodeDto.CoverTooLarge, StatusCodes.Status413PayloadTooLarge);
        }

        OwnAlbumResult result = await ownAlbums.SetCoverAsync(id, content, cancellationToken);
        return result is { IsOk: true, Item: { } item } ? Ok(item.ToDto()) : Failure(result);
    }

    /// <summary>
    /// Removes the cover of an own album. A cover already written into the album folder stays there. Succeeds also
    /// without a cover.
    /// </summary>
    /// <param name="id">Id of the own album.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The own album has no cover.</response>
    /// <response code="404">The own album does not exist (code <c>organize.own_album_not_found</c>).</response>
    [HttpDelete("{id:guid}/cover", Name = "DeleteOwnAlbumCover")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DeleteOwnAlbumCoverAsync(Guid id, CancellationToken cancellationToken)
    {
        OwnAlbumResult result = await ownAlbums.RemoveCoverAsync(id, cancellationToken);
        return result.IsOk ? NoContent() : Failure(result);
    }

    private ObjectResult NotFoundProblem()
    {
        return this.Problem(ProblemCodeDto.OwnAlbumNotFound, StatusCodes.Status404NotFound);
    }

    private ObjectResult Failure(OwnAlbumResult result)
    {
        return result.Status switch
        {
            OwnAlbumResultStatus.TrackNotFound => this.Problem(
                ProblemCodeDto.TrackNotFound,
                StatusCodes.Status404NotFound),
            OwnAlbumResultStatus.TrackNoAudio => this.Problem(
                ProblemCodeDto.TrackNoAudio,
                StatusCodes.Status409Conflict),
            OwnAlbumResultStatus.TrackInOtherAlbum => this.Problem(
                ProblemCodeDto.OwnAlbumTrackInOtherAlbum,
                StatusCodes.Status409Conflict),
            OwnAlbumResultStatus.TooManyTracks => this.Problem(
                ProblemCodeDto.OwnAlbumTooManyTracks,
                StatusCodes.Status409Conflict),
            OwnAlbumResultStatus.CoverUnsupportedFormat => this.Problem(
                ProblemCodeDto.CoverUnsupportedFormat,
                StatusCodes.Status415UnsupportedMediaType),
            OwnAlbumResultStatus.CoverUnavailable => this.Problem(
                ProblemCodeDto.ServiceUnavailable,
                StatusCodes.Status503ServiceUnavailable),
            _ => NotFoundProblem(),
        };
    }

    /// <summary>
    /// Reads the request body into memory, or returns null as soon as it is longer than
    /// <paramref name="maxBytes"/>; a body without Content-Length (chunked) is checked while reading.
    /// </summary>
    private async Task<MemoryStream?> ReadBodyAsync(long maxBytes, CancellationToken cancellationToken)
    {
        MemoryStream content = new();
        byte[] buffer = new byte[BufferSize];
        int read;
        while ((read = await Request.Body.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (content.Length + read > maxBytes)
            {
                await content.DisposeAsync();
                return null;
            }

            content.Write(buffer, 0, read);
        }

        content.Position = 0;
        return content;
    }
}
