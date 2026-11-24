using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.ArtistPictures.Lookups;
using WaveMP.Core.Caching.Covers;
using WaveMP.Core.Library.Artists;
using WaveMP.Core.Library.Covers;
using WaveMP.Data.Projections;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The online lookup of artist pictures, for admins only: the MusicBrainz artists that may be the artist, the pictures
/// of the chosen one on Wikimedia Commons (through its Wikidata item, without API key) and, with the API key of the
/// admin, on fanart.tv, a preview of each and taking one over as the online picture of the artist. It is switched off
/// by default; while it is off, every endpoint answers 409 (code <c>artist.picture_lookup_disabled</c>) without
/// sending any request. Answers of the services are kept in the cache for 7 days by default. Pictures are only
/// referenced by the ids of their candidates, never by an address of a client, and every request of the server goes to
/// an https address of an allowed host only, also when a service redirects it.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/artists/{id:guid}")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class ArtistPictureLookupsController(
    IArtistPictureLookupService lookup,
    IArtistService artists) : ControllerBase
{
    /// <summary>
    /// How long the browser may keep the preview of a candidate.
    /// </summary>
    private const string PreviewCacheControl = "private, max-age=3600";

    /// <summary>
    /// Searches MusicBrainz for artists with the name of the artist, the most relevant first. Requests to MusicBrainz
    /// wait in the queue that keeps the shortest time between two of them. The artist name is sent to MusicBrainz.
    /// </summary>
    /// <param name="id">Id of the artist.</param>
    /// <param name="offset">Number of results to skip.</param>
    /// <param name="limit">Number of results, 1 to 25.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The matches; empty if MusicBrainz knows none. isRemembered marks the MusicBrainz artist
    /// an admin chose before.</response>
    /// <response code="400">Invalid offset or limit (code <c>request.invalid</c>).</response>
    /// <response code="404">The artist does not exist (code <c>artist.not_found</c>).</response>
    /// <response code="409">The online lookup is switched off (code <c>artist.picture_lookup_disabled</c>).</response>
    /// <response code="502">MusicBrainz cannot be reached or failed (code
    /// <c>artist.picture_lookup_unavailable</c>).</response>
    [HttpGet("musicbrainz-matches", Name = "ListArtistMusicBrainzMatches")]
    [ProducesResponseType<PageDto<MusicBrainzArtistMatchDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    public async Task<ActionResult<PageDto<MusicBrainzArtistMatchDto>>> ListArtistMusicBrainzMatchesAsync(
        Guid id,
        [FromQuery, Range(0, 10_000)] int offset = 0,
        [FromQuery, Range(1, IArtistPictureLookupService.MaxMatchLimit)] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        ArtistMatchesResult result = await lookup.FindMatchesAsync(id, offset, limit, cancellationToken);
        if (result.Status != ArtistLookupStatus.Found)
        {
            return LookupProblem(result.Status);
        }

        return Ok(new PageDto<MusicBrainzArtistMatchDto>
        {
            Items = [.. result.Matches.Select(match => match.ToDto(result.RememberedId))],
            Offset = offset,
            Limit = limit,
            Total = result.Total,
        });
    }

    /// <summary>
    /// Looks up the pictures of a MusicBrainz artist: on Wikimedia Commons through its Wikidata item (without API key)
    /// and, if the admin entered an API key, on fanart.tv. The MusicBrainz id is sent to MusicBrainz, Wikidata and
    /// fanart.tv, the file names to Wikimedia Commons. Nothing is written to the artist; the MusicBrainz artist is
    /// remembered only when a picture is taken over.
    /// </summary>
    /// <param name="id">Id of the artist.</param>
    /// <param name="musicBrainzArtistId">The MusicBrainz artist; the one an admin chose before if missing.</param>
    /// <param name="offset">Number of pictures to skip.</param>
    /// <param name="limit">Number of pictures, 1 to 50.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The pictures, those of Wikimedia Commons first, then those of fanart.tv by likes; empty
    /// if the services have none.</response>
    /// <response code="400">No musicBrainzArtistId and none remembered (code <c>request.invalid</c>,
    /// <c>musicBrainzArtistId</c>: <c>required</c>) or invalid offset or limit.</response>
    /// <response code="404">The artist does not exist (code <c>artist.not_found</c>).</response>
    /// <response code="409">The online lookup is switched off (code <c>artist.picture_lookup_disabled</c>).</response>
    /// <response code="502">The services cannot be reached or failed and no picture was found (code
    /// <c>artist.picture_lookup_unavailable</c>).</response>
    /// <response code="503">The cache cannot be written (code <c>service.unavailable</c>).</response>
    [HttpGet("online-picture-candidates", Name = "ListArtistPictureCandidates")]
    [ProducesResponseType<PageDto<ArtistPictureCandidateDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<PageDto<ArtistPictureCandidateDto>>> ListArtistPictureCandidatesAsync(
        Guid id,
        [FromQuery] Guid? musicBrainzArtistId = null,
        [FromQuery, Range(0, 10_000)] int offset = 0,
        [FromQuery, Range(1, 50)] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        ArtistCandidatesResult result = await lookup.FindCandidatesAsync(id, musicBrainzArtistId, cancellationToken);
        if (result.Status == ArtistLookupStatus.MusicBrainzArtistMissing)
        {
            ModelState.AddValidationError("musicBrainzArtistId", ValidationErrorCodeDto.Required);
            return ValidationProblem();
        }

        if (result.Status != ArtistLookupStatus.Found)
        {
            return LookupProblem(result.Status);
        }

        return Ok(new PageDto<ArtistPictureCandidateDto>
        {
            Items = [.. result.Candidates.Skip(offset).Take(limit).Select(candidate => candidate.ToDto())],
            Offset = offset,
            Limit = limit,
            Total = result.Candidates.Count,
        });
    }

    /// <summary>
    /// Returns the preview of a candidate as WebP of 300 or 500 pixels (the requested size rounded up; 500 if
    /// missing), keeping its shape, so the admin can see and crop it before taking it over. The server downloads the
    /// picture once and keeps the preview in the cache; the browser never contacts the service.
    /// </summary>
    /// <param name="id">Id of the artist.</param>
    /// <param name="candidateId">Id of the candidate.</param>
    /// <param name="size">Largest width and height in pixels the client shows the preview with (1 to 4096).</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The preview.</response>
    /// <response code="400">Invalid size (code <c>request.invalid</c>).</response>
    /// <response code="404">The artist does not exist (code <c>artist.not_found</c>) or the candidate does not exist,
    /// expired or belongs to another artist (code <c>artist.picture_candidate_not_found</c>).</response>
    /// <response code="409">The online lookup is switched off (code <c>artist.picture_lookup_disabled</c>).</response>
    /// <response code="502">The service cannot be reached or delivered no usable picture (code
    /// <c>artist.picture_lookup_unavailable</c>).</response>
    /// <response code="503">Pictures cannot be processed: ffmpeg is missing or the cache cannot be written (code
    /// <c>service.unavailable</c>).</response>
    [HttpGet("online-picture-candidates/{candidateId}/preview", Name = "GetArtistPictureCandidatePreview")]
    [ProducesResponseType<Stream>(StatusCodes.Status200OK, ImageFormats.WebP)]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> GetArtistPictureCandidatePreviewAsync(
        Guid id,
        string candidateId,
        [FromQuery, Range(CoverSizes.MinRequested, CoverSizes.MaxRequested)] int? size = null,
        CancellationToken cancellationToken = default)
    {
        ArtistCandidatePreview preview = await lookup.GetPreviewAsync(id, candidateId, size, cancellationToken);
        if (preview is { Status: ArtistLookupStatus.Found, Content: { } content })
        {
            Response.Headers.CacheControl = PreviewCacheControl;
            return File(content, ImageFormats.WebP);
        }

        return LookupProblem(preview.Status);
    }

    /// <summary>
    /// Takes a candidate over as the online picture of the artist: the server downloads the picture in full size
    /// (1280 pixels wide on Wikimedia Commons), checks that it is a JPEG, PNG or WebP picture, cuts the section out
    /// (the largest square in the middle without one), stores it as WebP in several sizes without its metadata
    /// together with its author, license and source, and remembers the MusicBrainz artist for the next lookup.
    /// Replaces an earlier online picture. It is shown unless there is a picture file in the artist folder or an
    /// uploaded picture.
    /// </summary>
    /// <param name="id">Id of the artist.</param>
    /// <param name="request">The candidate and the section.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The artist with its new online picture and its credit.</response>
    /// <response code="400">No or an invalid candidateId or a section out of range (code
    /// <c>request.invalid</c>).</response>
    /// <response code="404">The artist does not exist (code <c>artist.not_found</c>) or the candidate does not exist,
    /// expired or belongs to another artist (code <c>artist.picture_candidate_not_found</c>).</response>
    /// <response code="409">The online lookup is switched off (code <c>artist.picture_lookup_disabled</c>).</response>
    /// <response code="502">The service cannot be reached or delivered no usable picture (code
    /// <c>artist.picture_lookup_unavailable</c>).</response>
    /// <response code="503">Pictures cannot be processed: ffmpeg is missing or the data folder cannot be written
    /// (code <c>service.unavailable</c>).</response>
    [HttpPut("online-picture", Name = "TakeOverArtistOnlinePicture")]
    [Consumes("application/json")]
    [ProducesResponseType<ArtistDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ArtistDto>> TakeOverArtistOnlinePictureAsync(
        Guid id,
        TakeOverArtistPictureDto request,
        CancellationToken cancellationToken)
    {
        ArtistLookupStatus status = await lookup.TakeOverAsync(
            id,
            request.CandidateId,
            request.Crop.ToCrop(),
            cancellationToken);
        if (status != ArtistLookupStatus.Found)
        {
            return LookupProblem(status);
        }

        ArtistRow? artist = await artists.GetAsync(id, User.GetUserId(), cancellationToken);
        return artist is null
            ? this.Problem(ProblemCodeDto.ArtistNotFound, StatusCodes.Status404NotFound)
            : Ok(artist.ToDto());
    }

    private ObjectResult LookupProblem(ArtistLookupStatus status)
    {
        return status switch
        {
            ArtistLookupStatus.ArtistNotFound =>
                this.Problem(ProblemCodeDto.ArtistNotFound, StatusCodes.Status404NotFound),
            ArtistLookupStatus.CandidateNotFound =>
                this.Problem(ProblemCodeDto.ArtistPictureCandidateNotFound, StatusCodes.Status404NotFound),
            ArtistLookupStatus.Disabled =>
                this.Problem(ProblemCodeDto.ArtistPictureLookupDisabled, StatusCodes.Status409Conflict),
            ArtistLookupStatus.ProcessingUnavailable =>
                this.Problem(ProblemCodeDto.ServiceUnavailable, StatusCodes.Status503ServiceUnavailable),
            _ => this.Problem(ProblemCodeDto.ArtistPictureLookupUnavailable, StatusCodes.Status502BadGateway),
        };
    }
}
