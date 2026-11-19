using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Library.Covers;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.OnlineMetadata.Searches;
using WaveMP.Core.OnlineMetadata.Updates;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The online search for album info, for admins only: matches of songs and albums in MusicBrainz, the covers of
/// their releases from the Cover Art Archive and applying the fields the admin confirms. Nothing is written before
/// that. While the search is switched off in its settings, every endpoint answers 409 (code
/// <c>lookup.disabled</c>) without sending a request to either service.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class OnlineMetadataController(
    IOnlineMetadataSearchService search,
    IOnlineMetadataApplier applier) : ControllerBase
{
    /// <summary>
    /// How long the browser may keep the preview of a cover; the covers of a release rarely change.
    /// </summary>
    private const string CoverCacheControl = "private, max-age=3600";

    /// <summary>
    /// Searches a song in MusicBrainz by its title, artist, album and duration (from its tags, otherwise from the
    /// folder and file name) and returns the matches with their degree of agreement, the best first, next to its
    /// current tags. Requests to MusicBrainz wait in a queue that keeps the shortest time between two of them.
    /// </summary>
    /// <param name="id">Id of the song.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The matches; empty if MusicBrainz knows none.</response>
    /// <response code="404">The song does not exist (code <c>track.not_found</c>).</response>
    /// <response code="409">The online search is switched off (code <c>lookup.disabled</c>).</response>
    /// <response code="503">MusicBrainz cannot be reached or failed (code <c>lookup.unavailable</c>).</response>
    [HttpGet("api/v1/tracks/{id:guid}/online-matches", Name = "SearchTrackOnlineMatches")]
    [ProducesResponseType<OnlineTrackMatchesDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<OnlineTrackMatchesDto>> SearchTrackOnlineMatchesAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        TrackSearchResult result = await search.SearchTrackAsync(id, cancellationToken);
        if (result.Status == OnlineSearchStatus.Searched && result.Current is { } current)
        {
            return Ok(new OnlineTrackMatchesDto
            {
                Current = current.ToDto(),
                Matches = [.. result.Matches.Select(match => match.ToDto())],
            });
        }

        return SearchProblem(result.Status, ProblemCodeDto.TrackNotFound);
    }

    /// <summary>
    /// Searches an album in MusicBrainz by its title, album artist and number of songs and returns the releases that
    /// may be it with their degree of agreement, the best first, next to its current tags.
    /// </summary>
    /// <param name="id">Id of the album.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The matches; empty if MusicBrainz knows none.</response>
    /// <response code="404">The album does not exist (code <c>album.not_found</c>).</response>
    /// <response code="409">The online search is switched off (code <c>lookup.disabled</c>).</response>
    /// <response code="503">MusicBrainz cannot be reached or failed (code <c>lookup.unavailable</c>).</response>
    [HttpGet("api/v1/albums/{id:guid}/online-matches", Name = "SearchAlbumOnlineMatches")]
    [ProducesResponseType<OnlineAlbumMatchesDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<OnlineAlbumMatchesDto>> SearchAlbumOnlineMatchesAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        AlbumSearchResult result = await search.SearchAlbumAsync(id, cancellationToken);
        if (result.Status == OnlineSearchStatus.Searched && result.Current is { } current)
        {
            return Ok(new OnlineAlbumMatchesDto
            {
                Current = current.Values.ToDto(),
                TrackCount = current.TrackCount,
                EditableTrackCount = current.EditableTrackCount,
                Matches = [.. result.Matches.Select(match => match.ToDto())],
            });
        }

        return SearchProblem(result.Status, ProblemCodeDto.AlbumNotFound);
    }

    /// <summary>
    /// Returns the front cover of a release from the Cover Art Archive as preview, before it is applied.
    /// </summary>
    /// <param name="releaseId">The MusicBrainz id of the release.</param>
    /// <param name="size">Size of the preview; small by default.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The picture.</response>
    /// <response code="400">Invalid size (code <c>request.invalid</c>).</response>
    /// <response code="404">The release has no cover (code <c>lookup.cover_not_found</c>).</response>
    /// <response code="409">The online search is switched off (code <c>lookup.disabled</c>).</response>
    /// <response code="503">The Cover Art Archive cannot be reached or failed (code
    /// <c>lookup.unavailable</c>).</response>
    [HttpGet("api/v1/online-releases/{releaseId:guid}/cover", Name = "GetOnlineReleaseCover")]
    [ProducesResponseType<Stream>(
        StatusCodes.Status200OK,
        ImageFormats.Jpeg,
        ImageFormats.Png,
        ImageFormats.WebP,
        ImageFormats.Gif,
        ImageFormats.Bmp)]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> GetOnlineReleaseCoverAsync(
        Guid releaseId,
        [FromQuery] OnlineCoverSizeDto size = OnlineCoverSizeDto.Small,
        CancellationToken cancellationToken = default)
    {
        CoverArtSize requested = size == OnlineCoverSizeDto.Large ? CoverArtSize.Large : CoverArtSize.Small;
        OnlineCoverPreview preview = await search.GetCoverAsync(releaseId, requested, cancellationToken);
        if (preview is { Status: OnlineStatus.Found, Picture: { } picture, ContentType: { } contentType })
        {
            Response.Headers.CacheControl = CoverCacheControl;
            return File(picture, contentType);
        }

        return preview.Status switch
        {
            OnlineStatus.NotFound => this.Problem(ProblemCodeDto.LookupCoverNotFound, StatusCodes.Status404NotFound),
            OnlineStatus.Disabled => this.Problem(ProblemCodeDto.LookupDisabled, StatusCodes.Status409Conflict),
            _ => this.Problem(ProblemCodeDto.LookupUnavailable, StatusCodes.Status503ServiceUnavailable),
        };
    }

    /// <summary>
    /// Applies the fields of a match the admin confirmed to a song: the tags through the metadata editor (into the
    /// audio file only, its current tags saved as backup first) and the front cover of a release, stored following
    /// the cover rules: in the media folder if it can be written, otherwise in the data folder. The cover is
    /// downloaded first, so a failing Cover Art Archive changes nothing.
    /// </summary>
    /// <param name="id">Id of the song.</param>
    /// <param name="request">The tags and the release of the cover.</param>
    /// <param name="cancellationToken">Aborts the request before anything is written.</param>
    /// <response code="200">The metadata of the song read again and where the cover went.</response>
    /// <response code="400">Invalid values (code <c>request.invalid</c>).</response>
    /// <response code="404">The song does not exist (code <c>track.not_found</c>) or the release has no cover (code
    /// <c>lookup.cover_not_found</c>).</response>
    /// <response code="409">The online search is switched off (code <c>lookup.disabled</c>), the song has no audio
    /// file (code <c>track.no_audio</c>) or its audio file cannot be written (code
    /// <c>track.not_writable</c>).</response>
    /// <response code="503">The Cover Art Archive cannot be reached or failed (code
    /// <c>lookup.unavailable</c>).</response>
    [HttpPost("api/v1/tracks/{id:guid}/online-metadata", Name = "ApplyTrackOnlineMetadata")]
    [Consumes("application/json")]
    [ProducesResponseType<OnlineTrackApplyResultDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<OnlineTrackApplyResultDto>> ApplyTrackOnlineMetadataAsync(
        Guid id,
        ApplyTrackOnlineMetadataDto request,
        CancellationToken cancellationToken)
    {
        TagChanges changes = request.Tags?.ToChanges() ?? new TagChanges();
        TrackApplyResult result =
            await applier.ApplyToTrackAsync(id, changes, request.CoverReleaseId, cancellationToken);
        if (result is { Error: null, Metadata: { } metadata })
        {
            return Ok(new OnlineTrackApplyResultDto { Metadata = metadata.ToDto(), Cover = result.Cover.ToDto() });
        }

        return ApplyProblem(result.Error);
    }

    /// <summary>
    /// Applies the album fields of a match the admin confirmed (album, album artist, year, genre) to every song of an
    /// album whose audio file can be written, each through the metadata editor with a backup first, and the front
    /// cover of a release as cover of the album in the folder of the album, otherwise in the data folder.
    /// </summary>
    /// <param name="id">Id of the album.</param>
    /// <param name="request">The tags and the release of the cover.</param>
    /// <param name="cancellationToken">Aborts the request before anything is written.</param>
    /// <response code="200">How many songs were written and where the cover went.</response>
    /// <response code="400">Invalid values (code <c>request.invalid</c>).</response>
    /// <response code="404">The album does not exist (code <c>album.not_found</c>) or the release has no cover (code
    /// <c>lookup.cover_not_found</c>).</response>
    /// <response code="409">The online search is switched off (code <c>lookup.disabled</c>).</response>
    /// <response code="503">The Cover Art Archive cannot be reached or failed (code
    /// <c>lookup.unavailable</c>).</response>
    [HttpPost("api/v1/albums/{id:guid}/online-metadata", Name = "ApplyAlbumOnlineMetadata")]
    [Consumes("application/json")]
    [ProducesResponseType<OnlineAlbumApplyResultDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<OnlineAlbumApplyResultDto>> ApplyAlbumOnlineMetadataAsync(
        Guid id,
        ApplyAlbumOnlineMetadataDto request,
        CancellationToken cancellationToken)
    {
        TagChanges changes = request.Tags?.ToChanges() ?? new TagChanges();
        AlbumApplyResult result =
            await applier.ApplyToAlbumAsync(id, changes, request.CoverReleaseId, cancellationToken);
        if (result is { Error: null, AlbumId: { } albumId })
        {
            return Ok(new OnlineAlbumApplyResultDto
            {
                AlbumId = albumId,
                TracksWritten = result.TracksWritten,
                TracksSkipped = result.TracksSkipped,
                Cover = result.Cover.ToDto(),
            });
        }

        return ApplyProblem(result.Error);
    }

    private ObjectResult SearchProblem(OnlineSearchStatus status, ProblemCodeDto notFound)
    {
        return status switch
        {
            OnlineSearchStatus.Disabled => this.Problem(ProblemCodeDto.LookupDisabled, StatusCodes.Status409Conflict),
            OnlineSearchStatus.Unavailable =>
                this.Problem(ProblemCodeDto.LookupUnavailable, StatusCodes.Status503ServiceUnavailable),
            _ => this.Problem(notFound, StatusCodes.Status404NotFound),
        };
    }

    private ObjectResult ApplyProblem(OnlineApplyError? error)
    {
        return error switch
        {
            OnlineApplyError.Disabled => this.Problem(ProblemCodeDto.LookupDisabled, StatusCodes.Status409Conflict),
            OnlineApplyError.AlbumNotFound => this.Problem(ProblemCodeDto.AlbumNotFound, StatusCodes.Status404NotFound),
            OnlineApplyError.NoAudio => this.Problem(ProblemCodeDto.TrackNoAudio, StatusCodes.Status409Conflict),
            OnlineApplyError.NotWritable => this.Problem(ProblemCodeDto.TrackNotWritable, StatusCodes.Status409Conflict),
            OnlineApplyError.CoverNotFound =>
                this.Problem(ProblemCodeDto.LookupCoverNotFound, StatusCodes.Status404NotFound),
            OnlineApplyError.Unavailable =>
                this.Problem(ProblemCodeDto.LookupUnavailable, StatusCodes.Status503ServiceUnavailable),
            _ => this.Problem(ProblemCodeDto.TrackNotFound, StatusCodes.Status404NotFound),
        };
    }
}
