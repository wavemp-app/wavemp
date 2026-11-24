using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using WaveMP.Core.ArtistPictures.Options;
using WaveMP.Core.ArtistPictures.Pictures;
using WaveMP.Core.Caching.Covers;
using WaveMP.Core.Library.Artists;
using WaveMP.Core.Library.Covers;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Filters;
using WaveMP.Service.Mappings;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The pictures of the artists: the picture file in the artist folder, a picture uploaded by an admin and a picture an
/// admin took over from the online lookup, shown in this order. Every profile sees the shown picture; only admins
/// see, upload and remove the stored pictures.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/artists/{id:guid}")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class ArtistPicturesController(
    IArtistPictureService pictures,
    IArtistService artists,
    IOptions<ArtistPictureOptions> options) : ControllerBase
{
    /// <summary>
    /// Returns the shown picture of an artist: the picture file in its folder, scaled to fit into a square of 96, 192,
    /// 300, 600, 1000 or 2000 pixels (the requested size rounded up; never enlarged; as stored if it cannot be
    /// scaled), otherwise the uploaded, otherwise the online picture as square WebP of 96, 300 or 1000 pixels. Every
    /// picture carries a strong ETag of its version and size, Last-Modified and Cache-Control: private, no-cache; a
    /// request with a matching If-None-Match returns 304. Artists without a picture of their own answer 404; clients
    /// show the collage of picture.albumIds instead.
    /// </summary>
    /// <param name="id">Id of the artist.</param>
    /// <param name="size">Largest width and height in pixels the client shows the picture with (1 to 4096), its width
    /// in CSS pixels times the pixel density of the screen. 1000 if missing.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The picture.</response>
    /// <response code="400">Invalid size (code <c>request.invalid</c>).</response>
    /// <response code="404">The artist does not exist (code <c>artist.not_found</c>) or has no picture of its own
    /// (code <c>artist.picture_not_found</c>).</response>
    [HttpGet("picture", Name = "GetArtistPicture")]
    [ProducesResponseType<Stream>(
        StatusCodes.Status200OK,
        ImageFormats.WebP,
        ImageFormats.Jpeg,
        ImageFormats.Png,
        ImageFormats.Gif,
        ImageFormats.Bmp)]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetArtistPictureAsync(
        Guid id,
        [FromQuery, Range(CoverSizes.MinRequested, CoverSizes.MaxRequested)] int? size = null,
        CancellationToken cancellationToken = default)
    {
        CoverDeliveryResult result = await pictures.GetShownAsync(id, size, cancellationToken);
        return this.Cover(result, ProblemCodeDto.ArtistNotFound, ProblemCodeDto.ArtistPictureNotFound);
    }

    /// <summary>
    /// Returns the uploaded picture of an artist, for admins only, also while the picture file in the artist folder
    /// hides it: square WebP of 96, 300 or 1000 pixels (the requested size rounded up; 1000 if missing), with ETag
    /// and 304 like GetArtistPicture.
    /// </summary>
    /// <param name="id">Id of the artist.</param>
    /// <param name="size">Largest width and height in pixels the client shows the picture with (1 to 4096).</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The picture.</response>
    /// <response code="400">Invalid size (code <c>request.invalid</c>).</response>
    /// <response code="401">No valid session.</response>
    /// <response code="403">The profile of the session is not an admin.</response>
    /// <response code="404">The artist does not exist (code <c>artist.not_found</c>) or has no uploaded picture
    /// (code <c>artist.picture_not_found</c>).</response>
    [HttpGet("uploaded-picture", Name = "GetArtistUploadedPicture")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, ImageFormats.WebP)]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<IActionResult> GetArtistUploadedPictureAsync(
        Guid id,
        [FromQuery, Range(CoverSizes.MinRequested, CoverSizes.MaxRequested)] int? size = null,
        CancellationToken cancellationToken = default)
    {
        return GetStoredAsync(id, ArtistPictureKind.Uploaded, size, cancellationToken);
    }

    /// <summary>
    /// Uploads a picture of an artist, for admins only: a JPEG, PNG or WebP picture in <c>file</c> (10 MiB at most by
    /// default), recognized by its content. The server cuts the largest square out of its middle, stores it as WebP
    /// in several sizes without its metadata and discards the uploaded file. Replaces an earlier uploaded picture. It
    /// is shown unless there is a picture file in the artist folder.
    /// </summary>
    /// <param name="id">Id of the artist.</param>
    /// <param name="request">The picture.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The artist with its new picture.</response>
    /// <response code="400">No <c>file</c> (code <c>request.invalid</c>, <c>file</c>: <c>required</c>).</response>
    /// <response code="401">No valid session.</response>
    /// <response code="403">The profile of the session is not an admin.</response>
    /// <response code="404">The artist does not exist (code <c>artist.not_found</c>).</response>
    /// <response code="413">The picture is too large (code <c>artist.picture_too_large</c>).</response>
    /// <response code="415">The picture is not a JPEG, PNG or WebP (code
    /// <c>artist.picture_unsupported_format</c>).</response>
    /// <response code="422">The picture is damaged (code <c>artist.picture_unreadable</c>).</response>
    /// <response code="503">Pictures cannot be processed: ffmpeg is missing or the data folder cannot be written
    /// (code <c>service.unavailable</c>).</response>
    [HttpPut("uploaded-picture", Name = "UploadArtistPicture")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [Consumes("multipart/form-data")]
    [ArtistPictureUploadLimit]
    [ProducesResponseType<ArtistDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status415UnsupportedMediaType, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ArtistDto>> UploadArtistPictureAsync(
        Guid id,
        [FromForm] UploadArtistPictureDto request,
        CancellationToken cancellationToken)
    {
        if (request.File is not { } file)
        {
            ModelState.AddValidationError("file", ValidationErrorCodeDto.Required);
            return ValidationProblem();
        }

        if (file.Length > options.Value.MaxUploadBytes)
        {
            return this.Problem(ProblemCodeDto.ArtistPictureTooLarge, StatusCodes.Status413PayloadTooLarge);
        }

        ArtistPictureUploadStatus status;
        await using (Stream content = file.OpenReadStream())
        {
            status = await pictures.UploadAsync(id, content, cancellationToken);
        }

        switch (status)
        {
            case ArtistPictureUploadStatus.Saved:
                return await ArtistAsync(id, cancellationToken);
            case ArtistPictureUploadStatus.UnsupportedFormat:
                return this.Problem(
                    ProblemCodeDto.ArtistPictureUnsupportedFormat,
                    StatusCodes.Status415UnsupportedMediaType);
            case ArtistPictureUploadStatus.Unreadable:
                return this.Problem(
                    ProblemCodeDto.ArtistPictureUnreadable,
                    StatusCodes.Status422UnprocessableEntity);
            case ArtistPictureUploadStatus.Unavailable:
                return this.Problem(ProblemCodeDto.ServiceUnavailable, StatusCodes.Status503ServiceUnavailable);
            default:
                return this.Problem(ProblemCodeDto.ArtistNotFound, StatusCodes.Status404NotFound);
        }
    }

    /// <summary>
    /// Removes the uploaded picture of an artist, for admins only. Succeeds also without one.
    /// </summary>
    /// <param name="id">Id of the artist.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The artist has no uploaded picture.</response>
    /// <response code="401">No valid session.</response>
    /// <response code="403">The profile of the session is not an admin.</response>
    /// <response code="404">The artist does not exist (code <c>artist.not_found</c>).</response>
    [HttpDelete("uploaded-picture", Name = "DeleteArtistUploadedPicture")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<IActionResult> DeleteArtistUploadedPictureAsync(Guid id, CancellationToken cancellationToken)
    {
        return RemoveAsync(id, ArtistPictureKind.Uploaded, cancellationToken);
    }

    /// <summary>
    /// Returns the online picture of an artist, for admins only, also while another source hides it: square WebP of
    /// 96, 300 or 1000 pixels (the requested size rounded up; 1000 if missing), with ETag and 304 like
    /// GetArtistPicture.
    /// </summary>
    /// <param name="id">Id of the artist.</param>
    /// <param name="size">Largest width and height in pixels the client shows the picture with (1 to 4096).</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The picture.</response>
    /// <response code="400">Invalid size (code <c>request.invalid</c>).</response>
    /// <response code="401">No valid session.</response>
    /// <response code="403">The profile of the session is not an admin.</response>
    /// <response code="404">The artist does not exist (code <c>artist.not_found</c>) or has no online picture (code
    /// <c>artist.picture_not_found</c>).</response>
    [HttpGet("online-picture", Name = "GetArtistOnlinePicture")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, ImageFormats.WebP)]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<IActionResult> GetArtistOnlinePictureAsync(
        Guid id,
        [FromQuery, Range(CoverSizes.MinRequested, CoverSizes.MaxRequested)] int? size = null,
        CancellationToken cancellationToken = default)
    {
        return GetStoredAsync(id, ArtistPictureKind.Online, size, cancellationToken);
    }

    /// <summary>
    /// Removes the online picture of an artist with its credit, for admins only. Succeeds also without one.
    /// </summary>
    /// <param name="id">Id of the artist.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The artist has no online picture.</response>
    /// <response code="401">No valid session.</response>
    /// <response code="403">The profile of the session is not an admin.</response>
    /// <response code="404">The artist does not exist (code <c>artist.not_found</c>).</response>
    [HttpDelete("online-picture", Name = "DeleteArtistOnlinePicture")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public Task<IActionResult> DeleteArtistOnlinePictureAsync(Guid id, CancellationToken cancellationToken)
    {
        return RemoveAsync(id, ArtistPictureKind.Online, cancellationToken);
    }

    private async Task<IActionResult> GetStoredAsync(
        Guid id,
        ArtistPictureKind kind,
        int? size,
        CancellationToken cancellationToken)
    {
        CoverDeliveryResult result = await pictures.GetStoredAsync(id, kind, size, cancellationToken);
        return this.Cover(result, ProblemCodeDto.ArtistNotFound, ProblemCodeDto.ArtistPictureNotFound);
    }

    private async Task<IActionResult> RemoveAsync(
        Guid id,
        ArtistPictureKind kind,
        CancellationToken cancellationToken)
    {
        return await pictures.RemoveAsync(id, kind, cancellationToken)
            ? NoContent()
            : this.Problem(ProblemCodeDto.ArtistNotFound, StatusCodes.Status404NotFound);
    }

    private async Task<ActionResult<ArtistDto>> ArtistAsync(Guid id, CancellationToken cancellationToken)
    {
        ArtistRow? artist = await artists.GetAsync(id, User.GetUserId(), cancellationToken);
        return artist is null
            ? this.Problem(ProblemCodeDto.ArtistNotFound, StatusCodes.Status404NotFound)
            : Ok(artist.ToDto());
    }
}
