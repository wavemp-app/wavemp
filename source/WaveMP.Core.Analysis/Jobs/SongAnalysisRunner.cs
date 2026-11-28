using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Analysis.Options;
using WaveMP.Core.Analysis.Services;
using WaveMP.Core.Transcoding.Services;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Analysis.Jobs;

/// <summary>
/// Goes through the tracks whose primary audio file or video was not analyzed yet, changed or was analyzed by an older
/// version (newest tracks first, in batches) and analyzes them one after another. Before every track it waits while
/// transcodes run, so the analysis never slows down a song or video that is being played; ffmpeg itself runs at the
/// lowest priority. Every track is stored when it is done, so a stopped run goes on where it stopped.
/// </summary>
internal sealed class SongAnalysisRunner(
    ISongAnalysisRepository repository,
    ISongAnalyzer analyzer,
    ITranscodingService transcoding,
    IOptions<AnalysisOptions> options,
    TimeProvider timeProvider,
    ILogger<SongAnalysisRunner> logger) : ISongAnalysisRunner
{
    /// <summary>
    /// Tracks read from the database at once.
    /// </summary>
    internal const int BatchSize = 20;

    /// <summary>
    /// A track that is still waiting after this many attempts in a run is left for the next run.
    /// </summary>
    internal const int MaxAttempts = 2;

    public async Task<SongAnalysisJobResult> RunAsync(CancellationToken cancellationToken = default)
    {
        Dictionary<Guid, int> attempts = [];
        int analyzed = 0;
        int needReview = 0;
        int failed = 0;
        while (true)
        {
            Guid[] excluded = [.. attempts.Where(pair => pair.Value >= MaxAttempts).Select(pair => pair.Key)];
            IReadOnlyList<Guid> batch = await repository.ListTracksToAnalyzeAsync(
                AnalysisOptions.Version,
                BatchSize,
                excluded,
                cancellationToken);
            // A track that stays in the list (e.g. one whose analysis cannot be stored) ends the run, never loops.
            Guid[] open = [.. batch.Where(trackId => attempts.GetValueOrDefault(trackId) < MaxAttempts)];
            if (open.Length == 0)
            {
                break;
            }

            foreach (Guid trackId in open)
            {
                await WaitForTranscodesAsync(cancellationToken);
                attempts[trackId] = attempts.GetValueOrDefault(trackId) + 1;
                try
                {
                    SongAnalysisResult result = await analyzer.AnalyzeAsync(trackId, cancellationToken);
                    if (result.Outcome == SongAnalysisOutcome.Analyzed)
                    {
                        analyzed++;
                        needReview += result.NeedsReview ? 1 : 0;
                        failed += result.Failed ? 1 : 0;
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    failed++;
                    attempts[trackId] = MaxAttempts;
                    logger.LogWarning(exception, "The song analysis of the track {TrackId} failed", trackId);
                }
            }
        }

        logger.LogInformation(
            "Song analysis: {Analyzed} tracks analyzed, {NeedReview} need a look, {Failed} could not be read",
            analyzed,
            needReview,
            failed);
        return new SongAnalysisJobResult(analyzed, needReview, failed);
    }

    /// <summary>
    /// Waits while transcodes run, at most <see cref="AnalysisOptions.MaxTranscodeWait"/>.
    /// </summary>
    private async Task WaitForTranscodesAsync(CancellationToken cancellationToken)
    {
        AnalysisOptions analysis = options.Value;
        TimeSpan waited = TimeSpan.Zero;
        while (waited < analysis.MaxTranscodeWait && transcoding.GetStatus().RunningJobs > 0)
        {
            await Task.Delay(analysis.TranscodeWaitInterval, timeProvider, cancellationToken);
            waited += analysis.TranscodeWaitInterval;
        }
    }
}
