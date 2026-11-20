using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Caching.Covers;
using WaveMP.Core.Library.Covers;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The cover rules of albums and tracks, for admins only: whether the cover of the album or the cover of the track is
/// shown first. The most specific rule wins (track, album, then the global rule of the library settings); if the
/// preferred cover is missing, the other one is shown. The rules are stored in the database and stay across scans.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class CoverRulesController(ICoverRuleService rules, ICoverCacheService covers) : ControllerBase
{
    /// <summary>
    /// Returns the cover rule of an album, the global rule it inherits and the priority that applies to it.
    /// </summary>
    /// <param name="id">Id of the album.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The cover rule of the album.</response>
    /// <response code="404">The album does not exist (code <c>album.not_found</c>).</response>
    [HttpGet("albums/{id:guid}/cover-rule", Name = "GetAlbumCoverRule")]
    [ProducesResponseType<CoverRuleDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CoverRuleDto>> GetAlbumCoverRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        CoverRuleResult result = await rules.GetAlbumRuleAsync(id, cancellationToken);
        return result.IsFound ? Ok(result.ToDto()) : AlbumNotFound();
    }

    /// <summary>
    /// Sets the cover rule of an album; it applies to the album and to its tracks without a rule of their own. Inherit
    /// follows the global rule again. A change starts the job cover-cache, which prepares the covers shown now; the
    /// cover URLs stay the same and deliver the new picture with a new ETag.
    /// </summary>
    /// <param name="id">Id of the album.</param>
    /// <param name="request">The rule.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The rule is set.</response>
    /// <response code="400">The rule is missing or unknown (code <c>request.invalid</c>, field <c>rule</c>).</response>
    /// <response code="404">The album does not exist (code <c>album.not_found</c>).</response>
    [HttpPut("albums/{id:guid}/cover-rule", Name = "SetAlbumCoverRule")]
    [Consumes("application/json")]
    [ProducesResponseType<CoverRuleDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CoverRuleDto>> SetAlbumCoverRuleAsync(
        Guid id,
        UpdateCoverRuleDto request,
        CancellationToken cancellationToken)
    {
        CoverRuleResult result = await rules.SetAlbumRuleAsync(id, request.Rule!.Value.ToRule(), cancellationToken);
        return result.IsFound ? Ok(result.ToDto()) : AlbumNotFound();
    }

    /// <summary>
    /// Returns the cover an album would show with the given rule, without changing it, e.g. to preview a rule before
    /// it is set. The picture is delivered like GetAlbumCover: scaled as WebP with size, as stored without.
    /// </summary>
    /// <param name="id">Id of the album.</param>
    /// <param name="rule">The rule to preview; Inherit previews the global rule.</param>
    /// <param name="size">Largest width and height in pixels the client shows the picture with (1 to 4096), its width
    /// in CSS pixels times the pixel density of the screen; the server rounds it up to 96, 192, 300, 600, 1000 or
    /// 2000.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The picture.</response>
    /// <response code="400">The rule is missing or unknown, or the size is invalid (code <c>request.invalid</c>).
    /// </response>
    /// <response code="404">The album does not exist (code <c>album.not_found</c>) or has no cover (code
    /// <c>cover.not_found</c>).</response>
    [HttpGet("albums/{id:guid}/cover-rule/preview", Name = "GetAlbumCoverRulePreview")]
    [ProducesResponseType<Stream>(
        StatusCodes.Status200OK,
        ImageFormats.Jpeg,
        ImageFormats.Png,
        ImageFormats.WebP,
        ImageFormats.Gif,
        ImageFormats.Bmp)]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetAlbumCoverRulePreviewAsync(
        Guid id,
        [FromQuery, Required] CoverRuleOptionDto? rule,
        [FromQuery, Range(CoverSizes.MinRequested, CoverSizes.MaxRequested)] int? size = null,
        CancellationToken cancellationToken = default)
    {
        this.ValidateEnum(rule, nameof(rule));
        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        CoverDeliveryResult result = await covers.PreviewAlbumCoverAsync(
            id,
            rule!.Value.ToRule(),
            size,
            cancellationToken);
        return this.Cover(result, ProblemCodeDto.AlbumNotFound);
    }

    /// <summary>
    /// Returns the cover rule of a track, the rule it inherits (of its album, otherwise the global one) and the
    /// priority that applies to it.
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The cover rule of the track.</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>).</response>
    [HttpGet("tracks/{id:guid}/cover-rule", Name = "GetTrackCoverRule")]
    [ProducesResponseType<CoverRuleDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CoverRuleDto>> GetTrackCoverRuleAsync(Guid id, CancellationToken cancellationToken)
    {
        CoverRuleResult result = await rules.GetTrackRuleAsync(id, cancellationToken);
        return result.IsFound ? Ok(result.ToDto()) : TrackNotFound();
    }

    /// <summary>
    /// Sets the cover rule of a track; Inherit follows the rule of its album again. A cover the admin selected for the
    /// track (SetTrackFileSelection) still comes first. A change starts the job cover-cache, which prepares the covers
    /// shown now; the cover URLs stay the same and deliver the new picture with a new ETag.
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="request">The rule.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The rule is set.</response>
    /// <response code="400">The rule is missing or unknown (code <c>request.invalid</c>, field <c>rule</c>).</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>).</response>
    [HttpPut("tracks/{id:guid}/cover-rule", Name = "SetTrackCoverRule")]
    [Consumes("application/json")]
    [ProducesResponseType<CoverRuleDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CoverRuleDto>> SetTrackCoverRuleAsync(
        Guid id,
        UpdateCoverRuleDto request,
        CancellationToken cancellationToken)
    {
        CoverRuleResult result = await rules.SetTrackRuleAsync(id, request.Rule!.Value.ToRule(), cancellationToken);
        return result.IsFound ? Ok(result.ToDto()) : TrackNotFound();
    }

    /// <summary>
    /// Returns the cover a track would show with the given rule, without changing it, e.g. to preview a rule before it
    /// is set. The picture is delivered like GetTrackCover: scaled as WebP with size, as stored without.
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="rule">The rule to preview; Inherit previews the rule of its album.</param>
    /// <param name="size">Largest width and height in pixels the client shows the picture with (1 to 4096), its width
    /// in CSS pixels times the pixel density of the screen; the server rounds it up to 96, 192, 300, 600, 1000 or
    /// 2000.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The picture.</response>
    /// <response code="400">The rule is missing or unknown, or the size is invalid (code <c>request.invalid</c>).
    /// </response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>) or has no cover (code
    /// <c>cover.not_found</c>).</response>
    [HttpGet("tracks/{id:guid}/cover-rule/preview", Name = "GetTrackCoverRulePreview")]
    [ProducesResponseType<Stream>(
        StatusCodes.Status200OK,
        ImageFormats.Jpeg,
        ImageFormats.Png,
        ImageFormats.WebP,
        ImageFormats.Gif,
        ImageFormats.Bmp)]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetTrackCoverRulePreviewAsync(
        Guid id,
        [FromQuery, Required] CoverRuleOptionDto? rule,
        [FromQuery, Range(CoverSizes.MinRequested, CoverSizes.MaxRequested)] int? size = null,
        CancellationToken cancellationToken = default)
    {
        this.ValidateEnum(rule, nameof(rule));
        if (!ModelState.IsValid)
        {
            return ValidationProblem();
        }

        CoverDeliveryResult result = await covers.PreviewTrackCoverAsync(
            id,
            rule!.Value.ToRule(),
            size,
            cancellationToken);
        return this.Cover(result, ProblemCodeDto.TrackNotFound);
    }

    private ObjectResult AlbumNotFound()
    {
        return this.Problem(ProblemCodeDto.AlbumNotFound, StatusCodes.Status404NotFound);
    }

    private ObjectResult TrackNotFound()
    {
        return this.Problem(ProblemCodeDto.TrackNotFound, StatusCodes.Status404NotFound);
    }
}
