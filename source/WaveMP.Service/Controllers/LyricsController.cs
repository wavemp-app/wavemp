using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Core.Lyrics.Lookups;
using WaveMP.Core.Lyrics.Services;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The lyrics of tracks: from the lyrics file with the base name of the track (Song.lrc, also enhanced LRC with word
/// timestamps), from lyrics downloaded from LRCLIB or from the tags of the track (synchronized lyrics such as ID3 SYLT
/// first, then unsynchronized ones such as ID3 USLT or the Vorbis comment LYRICS). Admins download lyrics from LRCLIB
/// per track while the download is switched on in the library settings, and see or forget the stored result of the
/// last lookup of a track.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/tracks/{id:guid}/lyrics")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class LyricsController(
    ILyricsService lyrics,
    ILyricsDownloader downloader,
    ILyricsLookupService lookups) : ControllerBase
{
    /// <summary>
    /// Returns the lyrics of a track from the source of the highest priority that has lyrics: the lyrics file, then
    /// downloaded lyrics stored in the database, then the tags. Synchronized lyrics carry the start of every line (and
    /// of every word for word-synchronized lyrics) in milliseconds; static lyrics have no times.
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The lyrics.</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>) or has no lyrics (code
    /// <c>lyrics.not_found</c>).</response>
    [HttpGet(Name = "GetTrackLyrics")]
    [ProducesResponseType<LyricsDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<LyricsDto>> GetTrackLyricsAsync(Guid id, CancellationToken cancellationToken)
    {
        return ToResponse(await lyrics.GetAsync(id, cancellationToken));
    }

    /// <summary>
    /// Downloads the lyrics of a track from LRCLIB, for admins only, and returns the lyrics the track has afterwards.
    /// LRCLIB is asked by title, artist, album and duration; synchronized lyrics are preferred. The lyrics are saved as
    /// lyrics file with the base name of the track if its folder is writable, otherwise in the database. Lyrics of the
    /// tags or the database are replaced; a lyrics file never is. Requests to LRCLIB are spaced, so the answer may take
    /// a moment.
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The lyrics of the track after the download.</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>), LRCLIB has no lyrics for it (code
    /// <c>lyrics.download_not_found</c>) or lists it as instrumental (code <c>lyrics.instrumental</c>).</response>
    /// <response code="409">The download is switched off in the library settings (code
    /// <c>lyrics.download_disabled</c>) or the track has a lyrics file (code <c>lyrics.file_exists</c>).</response>
    /// <response code="503">LRCLIB cannot be reached, e.g. without an internet connection (code
    /// <c>lyrics.service_unavailable</c>), or refuses requests for a while (code <c>lyrics.rate_limited</c>, with
    /// Retry-After if LRCLIB names a time).</response>
    [HttpPost("download", Name = "DownloadTrackLyrics")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType<LyricsDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<LyricsDto>> DownloadTrackLyricsAsync(Guid id, CancellationToken cancellationToken)
    {
        LyricsDownloadResult result = await downloader.DownloadAsync(id, LyricsDownloadMode.Manual, cancellationToken);
        switch (result.Outcome)
        {
            case LyricsDownloadOutcome.SavedToFile or LyricsDownloadOutcome.SavedToDatabase:
                return ToResponse(await lyrics.GetAsync(id, cancellationToken));
            case LyricsDownloadOutcome.NotFound:
                return this.Problem(ProblemCodeDto.LyricsDownloadNotFound, StatusCodes.Status404NotFound);
            case LyricsDownloadOutcome.Instrumental:
                return this.Problem(ProblemCodeDto.LyricsInstrumental, StatusCodes.Status404NotFound);
            case LyricsDownloadOutcome.Disabled:
                return this.Problem(ProblemCodeDto.LyricsDownloadDisabled, StatusCodes.Status409Conflict);
            case LyricsDownloadOutcome.LyricsFileExists or LyricsDownloadOutcome.NotNeeded:
                return this.Problem(ProblemCodeDto.LyricsFileExists, StatusCodes.Status409Conflict);
            case LyricsDownloadOutcome.RateLimited:
                if (result.RetryAfter is { } retryAfter && retryAfter > TimeSpan.Zero)
                {
                    Response.Headers[HeaderNames.RetryAfter] =
                        Math.Ceiling(retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
                }

                return this.Problem(ProblemCodeDto.LyricsRateLimited, StatusCodes.Status503ServiceUnavailable);
            case LyricsDownloadOutcome.Unavailable:
                return this.Problem(ProblemCodeDto.LyricsServiceUnavailable, StatusCodes.Status503ServiceUnavailable);
            default:
                return this.Problem(ProblemCodeDto.TrackNotFound, StatusCodes.Status404NotFound);
        }
    }

    /// <summary>
    /// Returns the stored result of the last lookup of the lyrics of a track in LRCLIB, for admins only. The download
    /// job looks up a track whose lookup found no lyrics (not found or instrumental) again after a wait (configuration
    /// <c>Lyrics__LookUpAgainAfter</c>), or at once once the result is forgotten.
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The result of the last lookup.</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>) or has no stored result (code
    /// <c>lyrics.lookup_not_found</c>).</response>
    [HttpGet("lookup", Name = "GetTrackLyricsLookup")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType<LyricsLookupDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<LyricsLookupDto>> GetTrackLyricsLookupAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        LyricsLookupResult result = await lookups.GetAsync(id, cancellationToken);
        if (result.IsFound)
        {
            return Ok(result.Lookup.ToDto());
        }

        return result.Status == LyricsLookupStatus.TrackNotFound
            ? this.Problem(ProblemCodeDto.TrackNotFound, StatusCodes.Status404NotFound)
            : this.Problem(ProblemCodeDto.LyricsLookupNotFound, StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Forgets the stored result of the lookup of the lyrics of a track in LRCLIB if it found no lyrics (not found or
    /// instrumental), for admins only. The next run of the download job (<c>lyrics-download</c>) then looks the track
    /// up again at once instead of after the wait. Results that found lyrics are kept.
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The result is forgotten.</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>) or has no stored result (code
    /// <c>lyrics.lookup_not_found</c>).</response>
    /// <response code="409">The lookup found lyrics or the track already had lyrics (code
    /// <c>lyrics.lookup_found_lyrics</c>).</response>
    [HttpDelete("lookup", Name = "DeleteTrackLyricsLookup")]
    [Authorize(Policy = AuthorizationPolicies.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> DeleteTrackLyricsLookupAsync(Guid id, CancellationToken cancellationToken)
    {
        return await lookups.ForgetAsync(id, cancellationToken) switch
        {
            LyricsLookupForgetOutcome.Forgotten => NoContent(),
            LyricsLookupForgetOutcome.FoundLyrics =>
                this.Problem(ProblemCodeDto.LyricsLookupFoundLyrics, StatusCodes.Status409Conflict),
            LyricsLookupForgetOutcome.NotLookedUp =>
                this.Problem(ProblemCodeDto.LyricsLookupNotFound, StatusCodes.Status404NotFound),
            _ => this.Problem(ProblemCodeDto.TrackNotFound, StatusCodes.Status404NotFound),
        };
    }

    private ActionResult<LyricsDto> ToResponse(LyricsResult result)
    {
        return result.Status switch
        {
            LyricsStatus.Found when result.IsFound => Ok(result.Lyrics.ToDto()),
            LyricsStatus.TrackNotFound => this.Problem(ProblemCodeDto.TrackNotFound, StatusCodes.Status404NotFound),
            _ => this.Problem(ProblemCodeDto.LyricsNotFound, StatusCodes.Status404NotFound),
        };
    }
}
