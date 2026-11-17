using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Scanning.Selections;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The file selection of a track, for admins only: which audio, video and cover file the track uses instead of the
/// automatic choice by quality. The files to choose from are listed by ListTrackFiles.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/tracks/{id:guid}/file-selection")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class TrackFileSelectionsController(IFileSelectionService selections) : ControllerBase
{
    /// <summary>
    /// Returns the file selection of a track: per category (audio, video, cover) the file the track uses, the file the
    /// admin selected and its state, the file the quality rule chooses and the files to choose from.
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The file selection of the track.</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>).</response>
    [HttpGet(Name = "GetTrackFileSelection")]
    [ProducesResponseType<TrackFileSelectionDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TrackFileSelectionDto>> GetTrackFileSelectionAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        FileSelectionDetails? details = await selections.GetAsync(id, cancellationToken);
        return details is null
            ? this.Problem(ProblemCodeDto.TrackNotFound, StatusCodes.Status404NotFound)
            : Ok(details.ToDto());
    }

    /// <summary>
    /// Selects the audio, video and cover file of a track instead of the automatic choice by quality and applies the
    /// selection at once: the track plays the selected files, takes its metadata from the selected audio file and
    /// shows the selected cover before the cover of its album. Replaces the whole selection; a missing or null id
    /// leaves the category to the quality rule, and the id of the current selection keeps it (also a missing one).
    /// The selection stays across scans; if a selected file is deleted, the track falls back to the automatic choice
    /// and the selection becomes Missing.
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="request">The selected files.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="200">The file selection after the change.</response>
    /// <response code="400">A selected file is not a file of its category of the track (code
    /// <c>request.invalid</c>, field <c>audioFileId</c>, <c>videoFileId</c> or <c>coverFileId</c> with
    /// <c>invalid_value</c>).</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>).</response>
    [HttpPut(Name = "SetTrackFileSelection")]
    [Consumes("application/json")]
    [ProducesResponseType<TrackFileSelectionDto>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TrackFileSelectionDto>> SetTrackFileSelectionAsync(
        Guid id,
        UpdateTrackFileSelectionDto request,
        CancellationToken cancellationToken)
    {
        FileSelectionResult result = await selections.SetAsync(id, request.ToChoices(), cancellationToken);
        if (result.Status == FileSelectionStatus.Done)
        {
            return Ok(result.Details!.ToDto());
        }

        if (result.Status == FileSelectionStatus.Invalid)
        {
            foreach (FileSelectionCategory category in result.InvalidCategories ?? [])
            {
                ModelState.AddValidationError(FieldOf(category), ValidationErrorCodeDto.InvalidValue);
            }

            return ValidationProblem();
        }

        return this.Problem(ProblemCodeDto.TrackNotFound, StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Removes the whole file selection of a track, so the quality rule chooses all its files again, and applies it
    /// at once.
    /// </summary>
    /// <param name="id">Id of the track.</param>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="204">The selection is removed.</response>
    /// <response code="404">The track does not exist (code <c>track.not_found</c>).</response>
    [HttpDelete(Name = "ResetTrackFileSelection")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> ResetTrackFileSelectionAsync(Guid id, CancellationToken cancellationToken)
    {
        return await selections.ResetAsync(id, cancellationToken)
            ? NoContent()
            : this.Problem(ProblemCodeDto.TrackNotFound, StatusCodes.Status404NotFound);
    }

    private static string FieldOf(FileSelectionCategory category)
    {
        return category switch
        {
            FileSelectionCategory.Audio => "audioFileId",
            FileSelectionCategory.Video => "videoFileId",
            _ => "coverFileId",
        };
    }
}
