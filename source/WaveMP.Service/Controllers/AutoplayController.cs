using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Autoplay.Selections;
using WaveMP.Core.Autoplay.Services;
using WaveMP.Core.Streaming.Formats;
using WaveMP.Core.Streaming.Signatures;
using WaveMP.Core.Streaming.Sources;
using WaveMP.Core.Transcoding.Services;
using WaveMP.Data.Projections;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Signatures;

namespace WaveMP.Service.Controllers;

/// <summary>
/// Autoplay of the current profile: the songs that play when a list ends and repeat is off.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/me/autoplay")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
public sealed class AutoplayController(
    IAutoplayService autoplay,
    IStreamService streams,
    ITranscodingService transcoding,
    MediaUrlFactory urls) : ControllerBase
{
    /// <summary>
    /// Chooses the songs that play after a list (album, playlist or queue), in random order: songs of the artists of
    /// the list, an artist that appears more often in the list more often; if they are not enough, songs of the same
    /// genres and similar decades. Songs of the list and excluded songs never come; songs the profile played within
    /// the last 24 hours only when no other song matches. Favorites and songs with 4 or 5 stars come a bit more often,
    /// songs with 1 star never; songs that only have a music video only while the profile includes them (setting
    /// autoplay.includeVideoOnly). Every song comes with its playback for the formats of the client, as
    /// GetTrackPlayback answers, including the signed stream URL. Clients call this when a list ends while repeat is
    /// off and the autoplay of the profile (setting autoplay.enabled) is on, and again for more songs; the endpoint
    /// itself does not check the setting.
    /// </summary>
    /// <param name="request">The list and the formats of the client.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The next songs; empty if no matching song is left.</response>
    /// <response code="400">The request is invalid (code <c>request.invalid</c>), e.g. no or more than 500
    /// <c>trackIds</c> (<c>min_items</c>, <c>max_items</c>), more than 500 <c>excludedTrackIds</c>, a <c>count</c> out
    /// of range (<c>out_of_range</c>) or missing, too many or unknown <c>audioFormats</c>.</response>
    /// <response code="404">The profile was deleted in the meantime (code <c>user.not_found</c>).</response>
    [HttpPost("next", Name = "SelectMyAutoplayTracks")]
    [Consumes("application/json")]
    [ProducesResponseType<AutoplayResultDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AutoplayResultDto>> SelectMyAutoplayTracksAsync(
        AutoplayRequestDto request,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<TrackRow>? tracks = await autoplay.SelectAsync(
            User.GetUserId(),
            new AutoplayCommand(
                request.TrackIds,
                request.ExcludedTrackIds ?? [],
                request.Count ?? AutoplayLimits.DefaultCount),
            cancellationToken);
        if (tracks is null)
        {
            return this.Problem(ProblemCodeDto.UserNotFound, StatusCodes.Status404NotFound);
        }

        HashSet<AudioFormat> formats = [.. request.AudioFormats.Select(format => format.ToEntity())];
        List<AutoplayTrackDto> items = [];
        foreach (TrackRow track in tracks)
        {
            TrackPlaybackDto? playback = await DecideAsync(track.Id, formats, cancellationToken);
            if (playback is not null)
            {
                items.Add(new AutoplayTrackDto { Track = track.ToDto(), Playback = playback });
            }
        }

        Response.Headers.CacheControl = "no-store";
        return Ok(new AutoplayResultDto { Items = items });
    }

    /// <summary>
    /// The playback of a song as GetTrackPlayback decides it; <c>null</c> if the client cannot play it.
    /// </summary>
    private async Task<TrackPlaybackDto?> DecideAsync(
        Guid trackId,
        IReadOnlySet<AudioFormat> formats,
        CancellationToken cancellationToken)
    {
        PlaybackResult result = await streams.DecideAsync(trackId, formats, cancellationToken);
        if (!result.IsFound)
        {
            return null;
        }

        MediaUrl? stream = urls.CreatePlaybackUrl(
            HttpContext,
            trackId,
            result.Decision.Method,
            result.Source.Duration,
            transcoding.IsAvailable,
            MediaUrlTarget.Local);
        return stream is null ? null : result.Source.ToDto(result.Decision, stream);
    }
}
