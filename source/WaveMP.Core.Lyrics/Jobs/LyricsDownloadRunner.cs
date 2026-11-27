using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Jobs.Runs;
using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Core.Lyrics.Options;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Lyrics.Jobs;

/// <summary>
/// Goes through the tracks without lyrics file that were never looked up, then those whose lookup found no lyrics
/// longer than <see cref="LyricsOptions.LookUpAgainAfter"/> ago or with older rules than the current
/// <see cref="LrclibMatcher.Version"/>, and those that had lyrics in their tags at the check
/// (looked at again after that wait, or at once when a scan read their audio or video file after the check, because
/// the tags may have lost their lyrics; if they still carry lyrics, LRCLIB is not asked), oldest first, in batches,
/// and downloads their lyrics one after another (the requests are spaced by the request interval). If LRCLIB refuses
/// requests or cannot be reached, the track is tried again after a wait (as long as LRCLIB asks, within the limits of
/// the options); after the last attempt the run fails, and the next run goes on where it stopped. Switching the
/// download off stops the run after the current track.
/// </summary>
internal sealed class LyricsDownloadRunner(
    ILyricsRepository repository,
    ILyricsDownloader downloader,
    ISettingsSnapshot<LibrarySettings> settings,
    IOptions<LyricsOptions> options,
    TimeProvider timeProvider,
    ILogger<LyricsDownloadRunner> logger) : ILyricsDownloadRunner
{
    /// <summary>
    /// Tracks read from the database at once.
    /// </summary>
    internal const int BatchSize = 50;

    public async Task<LyricsDownloadJobResult> RunAsync(CancellationToken cancellationToken = default)
    {
        if (!settings.Current.DownloadLyrics)
        {
            throw new JobFailedException(
                JobErrorCode.LyricsDownloadDisabled,
                "The download of lyrics is switched off in the library settings.");
        }

        DateTimeOffset? lookUpAgainBefore =
            LookUpAgainBefore(timeProvider.GetUtcNow(), options.Value.LookUpAgainAfter);
        HashSet<Guid> handled = [];
        Counts counts = new();
        while (true)
        {
            IReadOnlyList<LyricsTrackRow> batch =
                await repository.ListTracksToLookUpAsync(
                    BatchSize,
                    handled,
                    lookUpAgainBefore,
                    LrclibMatcher.Version,
                    cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            foreach (LyricsTrackRow track in batch)
            {
                if (!settings.Current.DownloadLyrics)
                {
                    logger.LogInformation("The download of lyrics was switched off; the run stops");
                    return counts.ToResult(stopped: true);
                }

                handled.Add(track.TrackId);
                LyricsDownloadResult result = await DownloadAsync(track.TrackId, cancellationToken);
                counts.Add(result.Outcome);
            }
        }

        logger.LogInformation(
            "Lyrics looked up for {Tracks} tracks: {Downloaded} downloaded, {NotFound} not found, {Skipped} skipped",
            counts.Checked,
            counts.Downloaded,
            counts.NotFound,
            counts.Skipped);
        return counts.ToResult(stopped: false);
    }

    /// <summary>
    /// Tracks whose lookup found no lyrics or that already had lyrics before this time are looked up again; <c>null</c>
    /// if no track is looked up again after a wait (the wait is zero or less, or longer than the time since the
    /// earliest possible time).
    /// </summary>
    internal static DateTimeOffset? LookUpAgainBefore(DateTimeOffset now, TimeSpan lookUpAgainAfter)
    {
        if (lookUpAgainAfter <= TimeSpan.Zero || lookUpAgainAfter > now - DateTimeOffset.MinValue)
        {
            return null;
        }

        return now - lookUpAgainAfter;
    }

    /// <summary>
    /// How long to wait before the next attempt: as long as LRCLIB asks, otherwise the retry delay; at most the
    /// longest retry delay.
    /// </summary>
    internal static TimeSpan RetryDelay(TimeSpan? requested, LyricsOptions options)
    {
        TimeSpan delay = requested is { } asked && asked > TimeSpan.Zero ? asked : options.RetryDelay;
        return delay > options.MaxRetryDelay ? options.MaxRetryDelay : delay;
    }

    private async Task<LyricsDownloadResult> DownloadAsync(Guid trackId, CancellationToken cancellationToken)
    {
        LyricsOptions lyricsOptions = options.Value;
        int attempts = Math.Max(1, lyricsOptions.MaxAttempts);
        for (int attempt = 1; ; attempt++)
        {
            LyricsDownloadResult result =
                await downloader.DownloadAsync(trackId, LyricsDownloadMode.Automatic, cancellationToken);
            if (!result.IsTransient)
            {
                return result;
            }

            if (attempt >= attempts)
            {
                throw new JobFailedException(
                    JobErrorCode.LyricsServiceUnavailable,
                    $"LRCLIB did not answer after {attempts} attempts ({result.Outcome}).");
            }

            TimeSpan delay = RetryDelay(result.RetryAfter, lyricsOptions);
            logger.LogInformation(
                "LRCLIB did not answer ({Outcome}); next attempt in {Delay}",
                result.Outcome,
                delay);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, timeProvider, cancellationToken);
            }
        }
    }

    /// <summary>
    /// The numbers of a run.
    /// </summary>
    private sealed class Counts
    {
        public int Checked { get; private set; }

        public int Downloaded { get; private set; }

        public int NotFound { get; private set; }

        public int Skipped { get; private set; }

        public void Add(LyricsDownloadOutcome outcome)
        {
            Checked++;
            switch (outcome)
            {
                case LyricsDownloadOutcome.SavedToFile or LyricsDownloadOutcome.SavedToDatabase:
                    Downloaded++;
                    break;
                case LyricsDownloadOutcome.NotFound or LyricsDownloadOutcome.Instrumental:
                    NotFound++;
                    break;
                default:
                    Skipped++;
                    break;
            }
        }

        public LyricsDownloadJobResult ToResult(bool stopped)
        {
            return new LyricsDownloadJobResult(Checked, Downloaded, NotFound, Skipped, stopped);
        }
    }
}
