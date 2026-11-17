using Microsoft.Extensions.Logging;
using WaveMP.Core.Jobs.Definitions;
using WaveMP.Core.Jobs.Services;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Handlers;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Lyrics.Jobs;

/// <summary>
/// Starts the job <see cref="JobIds.LyricsDownload"/> when the admin switches the download of lyrics on, so the
/// tracks of the library are looked up without waiting for the next scan.
/// </summary>
internal sealed class LyricsDownloadSettingsHandler(IJobService jobs, ILogger<LyricsDownloadSettingsHandler> logger)
    : ISettingsChangeHandler<LibrarySettings>
{
    public async ValueTask OnSettingsChangedAsync(
        LibrarySettings previous,
        LibrarySettings current,
        CancellationToken cancellationToken)
    {
        if (previous.DownloadLyrics || !current.DownloadLyrics)
        {
            return;
        }

        JobRunStartResult started = await jobs.StartRunAsync(
            new JobRunRequest(JobIds.LyricsDownload, JobRunTrigger.Settings),
            cancellationToken);
        if (!started.IsSuccess)
        {
            logger.LogDebug("The lyrics are not downloaded after switching the download on: {Reason}", started.Error);
        }
    }
}
