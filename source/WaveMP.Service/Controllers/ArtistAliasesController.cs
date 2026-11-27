using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.ArtistPictures.Lookups;
using WaveMP.Core.Library.Artists;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Queries;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The aliases of the artists: further names the search finds an artist by, e.g. "Alecia Moore" for "P!nk". Search
/// results always show the name of the artist. Every profile sees the aliases; admins add and remove them and take
/// them from MusicBrainz. Aliases only serve the search, the scan never joins artists by them.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/artists/{id:guid}/aliases")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class ArtistAliasesController(
    IArtistAliasService aliases,
    IArtistAliasLookupService lookup) : ControllerBase
{
    private const string GetArtistAliasRoute = "GetArtistAlias";

    /// <summary>
    /// Lists the aliases of an artist, sorted by their normalized forms.
    /// </summary>
    /// <param name="id">Id of the artist.</param>
    /// <param name="offset">Number of aliases to skip.</param>
    /// <param name="limit">Maximum number of aliases of the page.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">One page of aliases.</response>
    /// <response code="400">Invalid query parameters (code <c>request.invalid</c>).</response>
    /// <response code="404">The artist does not exist (code <c>artist.not_found</c>).</response>
    [HttpGet(Name = "ListArtistAliases")]
    [ProducesResponseType<PageDto<ArtistAliasDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PageDto<ArtistAliasDto>>> ListArtistAliasesAsync(
        Guid id,
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit,
        CancellationToken cancellationToken = default)
    {
        Page<ArtistAliasRow>? page = await aliases.ListAsync(id, offset, limit, cancellationToken);
        return page is null
            ? this.Problem(ProblemCodeDto.ArtistNotFound, StatusCodes.Status404NotFound)
            : Ok(page.ToDto(alias => alias.ToDto()));
    }

    /// <summary>
    /// Returns an alias of an artist.
    /// </summary>
    /// <param name="id">Id of the artist.</param>
    /// <param name="aliasId">Id of the alias.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The alias.</response>
    /// <response code="404">The artist has no such alias (code <c>artist.alias_not_found</c>).</response>
    [HttpGet("{aliasId:guid}", Name = GetArtistAliasRoute)]
    [ProducesResponseType<ArtistAliasDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ArtistAliasDto>> GetArtistAliasAsync(
        Guid id,
        Guid aliasId,
        CancellationToken cancellationToken)
    {
        ArtistAliasRow? alias = await aliases.GetAsync(id, aliasId, cancellationToken);
        return alias is null
            ? this.Problem(ProblemCodeDto.ArtistAliasNotFound, StatusCodes.Status404NotFound)
            : Ok(alias.ToDto());
    }

    /// <summary>
    /// Adds an alias to an artist, for admins only. An alias of MusicBrainz that an admin removed comes back as an
    /// alias of the admin.
    /// </summary>
    /// <param name="id">Id of the artist.</param>
    /// <param name="request">The alias.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="201">The new alias; Location points to it.</response>
    /// <response code="400">No name or one of spaces only (code <c>request.invalid</c>, <c>name</c>:
    /// <c>required</c>) or a name that is too long.</response>
    /// <response code="403">The profile of the session is not an admin.</response>
    /// <response code="404">The artist does not exist (code <c>artist.not_found</c>).</response>
    /// <response code="409">The artist has the alias already or it is its name (code
    /// <c>artist.alias_exists</c>).</response>
    [HttpPost(Name = "CreateArtistAlias")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [Consumes("application/json")]
    [ProducesResponseType<ArtistAliasDto>(StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ArtistAliasDto>> CreateArtistAliasAsync(
        Guid id,
        CreateArtistAliasDto request,
        CancellationToken cancellationToken)
    {
        ArtistAliasAddResult result = await aliases.AddAsync(id, request.Name, cancellationToken);
        switch (result.Status)
        {
            case ArtistAliasAddStatus.Added:
                return CreatedAtRoute(
                    GetArtistAliasRoute,
                    new { id, aliasId = result.Alias!.Id },
                    result.Alias.ToDto());
            case ArtistAliasAddStatus.Empty:
                ModelState.AddValidationError("name", ValidationErrorCodeDto.Required);
                return ValidationProblem();
            case ArtistAliasAddStatus.Duplicate:
                return this.Problem(ProblemCodeDto.ArtistAliasExists, StatusCodes.Status409Conflict);
            default:
                return this.Problem(ProblemCodeDto.ArtistNotFound, StatusCodes.Status404NotFound);
        }
    }

    /// <summary>
    /// Removes an alias of an artist, for admins only. A removed alias of MusicBrainz is remembered, so taking the
    /// aliases from MusicBrainz again does not bring it back.
    /// </summary>
    /// <param name="id">Id of the artist.</param>
    /// <param name="aliasId">Id of the alias.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The alias is removed.</response>
    /// <response code="403">The profile of the session is not an admin.</response>
    /// <response code="404">The artist has no such alias (code <c>artist.alias_not_found</c>).</response>
    [HttpDelete("{aliasId:guid}", Name = "DeleteArtistAlias")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DeleteArtistAliasAsync(Guid id, Guid aliasId, CancellationToken cancellationToken)
    {
        return await aliases.RemoveAsync(id, aliasId, cancellationToken)
            ? NoContent()
            : this.Problem(ProblemCodeDto.ArtistAliasNotFound, StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Takes the aliases of an artist from MusicBrainz, for admins only: the aliases and the sort name of its
    /// MusicBrainz artist (from the tags of its songs or chosen in the online lookup of pictures) replace the aliases
    /// MusicBrainz named before; aliases of admins and aliases an admin removed stay as they are. Part of the online
    /// lookup of artists, so it only works while that is switched on. The MusicBrainz id is sent to MusicBrainz; the
    /// request waits in the queue that keeps the shortest time between two requests, and the answer is kept in the
    /// cache. Taking over an online picture also takes the aliases.
    /// </summary>
    /// <param name="id">Id of the artist.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The first page of the aliases of the artist afterwards.</response>
    /// <response code="403">The profile of the session is not an admin.</response>
    /// <response code="404">The artist does not exist (code <c>artist.not_found</c>).</response>
    /// <response code="409">The online lookup is switched off (code <c>artist.picture_lookup_disabled</c>) or the
    /// artist has no MusicBrainz artist (code <c>artist.musicbrainz_artist_missing</c>).</response>
    /// <response code="502">MusicBrainz cannot be reached or failed (code
    /// <c>artist.picture_lookup_unavailable</c>).</response>
    [HttpPost("musicbrainz-imports", Name = "ImportArtistAliasesFromMusicBrainz")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType<PageDto<ArtistAliasDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway, "application/problem+json")]
    public async Task<ActionResult<PageDto<ArtistAliasDto>>> ImportArtistAliasesFromMusicBrainzAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        ArtistAliasLookupResult result = await lookup.LookUpAsync(id, cancellationToken);
        return result.Status switch
        {
            ArtistLookupStatus.Found => Ok(new PageDto<ArtistAliasDto>
            {
                Items = [.. result.Aliases.Take(PageLimits.MaxLimit).Select(alias => alias.ToDto())],
                Offset = 0,
                Limit = PageLimits.MaxLimit,
                Total = result.Aliases.Count,
            }),
            ArtistLookupStatus.ArtistNotFound =>
                this.Problem(ProblemCodeDto.ArtistNotFound, StatusCodes.Status404NotFound),
            ArtistLookupStatus.Disabled =>
                this.Problem(ProblemCodeDto.ArtistPictureLookupDisabled, StatusCodes.Status409Conflict),
            ArtistLookupStatus.MusicBrainzArtistMissing =>
                this.Problem(ProblemCodeDto.ArtistMusicBrainzArtistMissing, StatusCodes.Status409Conflict),
            _ => this.Problem(ProblemCodeDto.ArtistPictureLookupUnavailable, StatusCodes.Status502BadGateway),
        };
    }
}
