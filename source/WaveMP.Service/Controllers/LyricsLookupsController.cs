using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Lyrics.Lookups;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The stored lookups of lyrics in LRCLIB that found no lyrics (not found or instrumental), over all tracks, for admins
/// only. The download job (<c>lyrics-download</c>) looks up such a track again after a wait (configuration
/// <c>Lyrics__LookUpAgainAfter</c>); forgetting the results makes its next run look the tracks up again at once. The
/// result of one track is read and forgotten at <c>/api/v1/tracks/{id}/lyrics/lookup</c>.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/lyrics-lookups")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class LyricsLookupsController(ILyricsLookupService lookups) : ControllerBase
{
    /// <summary>
    /// Returns how many tracks have a stored lookup that found no lyrics, and how long the download job waits before it
    /// looks them up again.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The numbers of the lookups without lyrics.</response>
    [HttpGet("summary", Name = "GetLyricsLookupSummary")]
    [ProducesResponseType<LyricsLookupSummaryDto>(StatusCodes.Status200OK, "application/json")]
    public async Task<ActionResult<LyricsLookupSummaryDto>> GetLyricsLookupSummaryAsync(
        CancellationToken cancellationToken)
    {
        return Ok((await lookups.GetSummaryAsync(cancellationToken)).ToDto());
    }

    /// <summary>
    /// Forgets the stored lookups of all tracks that found no lyrics (not found or instrumental), so the next run of
    /// the download job looks these tracks up again at once. Results that found lyrics are kept, also lyrics stored in
    /// the database.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The lookups without lyrics are forgotten.</response>
    [HttpDelete(Name = "DeleteLyricsLookups")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteLyricsLookupsAsync(CancellationToken cancellationToken)
    {
        await lookups.ForgetAllAsync(cancellationToken);
        return NoContent();
    }
}
