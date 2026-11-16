using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WaveMP.Core.Backups.Files;
using WaveMP.Core.Backups.Services;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Services;
using WaveMP.Data.Entities;
using WaveMP.Data.Queries;
using WaveMP.Service.Dtos;
using WaveMP.Service.Extensions;
using WaveMP.Service.Mappings;
using WaveMP.Service.Queries;
using WaveMP.Service.Tokens;

namespace WaveMP.Service.Controllers;

/// <summary>
/// The exports of the database in the backup folder (<c>/data/backups</c>), for admins only. WaveMP exports the
/// database every day (job <c>database-backup</c>, whose state, last and next run <c>/api/v1/jobs/database-backup</c>
/// returns) and before every update that changes the database. Time of day and number of kept exports are part of the
/// settings (<c>/api/v1/settings/backup</c>). There is no restore through the API.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/v1/backups")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
public sealed class BackupsController(IBackupService backups, IJobService jobs) : ControllerBase
{
    /// <summary>
    /// Lists the exports of the database, newest first.
    /// </summary>
    /// <param name="offset">Number of exports to skip.</param>
    /// <param name="limit">Maximum number of exports of the page.</param>
    /// <response code="200">One page of exports.</response>
    /// <response code="400">Invalid paging parameters (code <c>request.invalid</c>).</response>
    [HttpGet(Name = "ListBackups")]
    [ProducesResponseType<PageDto<BackupDto>>(StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType<ValidationProblemDto>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public ActionResult<PageDto<BackupDto>> ListBackups(
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, PageLimits.MaxLimit)] int limit = PageLimits.DefaultLimit)
    {
        Page<BackupFile> page = backups.List(offset, limit);
        return Ok(page.ToDto(file => file.ToDto()));
    }

    /// <summary>
    /// Exports the database now. The export runs in the background as a run of the job <c>database-backup</c>; when
    /// it has succeeded, the new export is listed and the oldest ones beyond the number of the settings are deleted.
    /// </summary>
    /// <param name="cancellationToken">Aborts the request.</param>
    /// <response code="202">The run is queued; Location points to it.</response>
    /// <response code="409">A backup is already queued or running (code <c>job.already_running</c>).</response>
    [HttpPost(Name = "CreateBackup")]
    [ProducesResponseType<JobRunDto>(StatusCodes.Status202Accepted, "application/json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<JobRunDto>> CreateBackupAsync(CancellationToken cancellationToken)
    {
        JobRunStartResult result = await jobs.StartRunAsync(
            new JobRunRequest(JobIds.DatabaseBackup, JobRunTrigger.Manual),
            cancellationToken);
        if (!result.IsSuccess)
        {
            return this.Problem(ProblemCodeDto.JobAlreadyRunning, StatusCodes.Status409Conflict);
        }

        return AcceptedAtRoute(
            JobsController.GetJobRunRoute,
            new { id = JobIds.DatabaseBackup, runId = result.Run.Id },
            result.Run.ToDto());
    }

    /// <summary>
    /// Deletes an export of the database.
    /// </summary>
    /// <param name="id">Id of the export, e.g. <c>wavemp-20261116T030000Z-schedule</c>.</param>
    /// <response code="204">The export is deleted.</response>
    /// <response code="404">There is no export with this id (code <c>backup.not_found</c>).</response>
    [HttpDelete("{id}", Name = "DeleteBackup")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public IActionResult DeleteBackup(string id)
    {
        return backups.Delete(id)
            ? NoContent()
            : this.Problem(ProblemCodeDto.BackupNotFound, StatusCodes.Status404NotFound);
    }
}
