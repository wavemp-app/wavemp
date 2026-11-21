using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WaveMP.Core.Lyrics.Options;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Lyrics.Lookups;

/// <summary>
/// Reads and forgets the stored lookups that found no lyrics. A forgotten lookup makes the track one that was never
/// looked up, which the download job looks up first.
/// </summary>
internal sealed class LyricsLookupService(
    ILyricsRepository repository,
    IOptions<LyricsOptions> options,
    ILogger<LyricsLookupService> logger) : ILyricsLookupService
{
    /// <summary>
    /// Results of lookups that found no lyrics; only these are forgotten.
    /// </summary>
    internal static readonly IReadOnlyList<LyricsDownloadStatus> ForgettableStatuses =
        [.. Enum.GetValues<LyricsDownloadStatus>().Where(LyricsLookupResult.IsForgettableStatus)];

    public async Task<LyricsLookupSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyDictionary<LyricsDownloadStatus, int> counts =
            await repository.CountDownloadsAsync(cancellationToken);
        return new LyricsLookupSummary(
            counts.GetValueOrDefault(LyricsDownloadStatus.NotFound),
            counts.GetValueOrDefault(LyricsDownloadStatus.Instrumental),
            LookUpAgainAfter(options.Value.LookUpAgainAfter));
    }

    public async Task<LyricsLookupResult> GetAsync(Guid trackId, CancellationToken cancellationToken = default)
    {
        LyricsTrackRow? track = await repository.FindTrackAsync(trackId, cancellationToken);
        if (track is null)
        {
            return LyricsLookupResult.TrackNotFound;
        }

        return track.Download is { } lookup ? LyricsLookupResult.Found(lookup) : LyricsLookupResult.NotLookedUp;
    }

    public async Task<int> ForgetAllAsync(CancellationToken cancellationToken = default)
    {
        int forgotten = await repository.DeleteDownloadsAsync(ForgettableStatuses, null, cancellationToken);
        logger.LogInformation(
            "Forgot {Count} lyrics lookups without lyrics; the next lyrics download looks the tracks up again",
            forgotten);
        return forgotten;
    }

    public async Task<LyricsLookupForgetOutcome> ForgetAsync(
        Guid trackId,
        CancellationToken cancellationToken = default)
    {
        LyricsTrackRow? track = await repository.FindTrackAsync(trackId, cancellationToken);
        if (track is null)
        {
            return LyricsLookupForgetOutcome.TrackNotFound;
        }

        if (track.Download is not { } lookup)
        {
            return LyricsLookupForgetOutcome.NotLookedUp;
        }

        if (!LyricsLookupResult.IsForgettableStatus(lookup.Status))
        {
            return LyricsLookupForgetOutcome.FoundLyrics;
        }

        // Only a result without lyrics is removed, also if a download replaced it in the meantime.
        if (await repository.DeleteDownloadsAsync(ForgettableStatuses, trackId, cancellationToken) == 0)
        {
            return (await GetAsync(trackId, cancellationToken)).Status switch
            {
                LyricsLookupStatus.TrackNotFound => LyricsLookupForgetOutcome.TrackNotFound,
                LyricsLookupStatus.Found => LyricsLookupForgetOutcome.FoundLyrics,
                _ => LyricsLookupForgetOutcome.NotLookedUp,
            };
        }

        logger.LogInformation("Forgot the lyrics lookup of track {TrackId}", trackId);
        return LyricsLookupForgetOutcome.Forgotten;
    }

    /// <summary>
    /// The wait of the download job before it looks up a track again whose lookup found no lyrics; <c>null</c> if it
    /// never looks such a track up again after a wait.
    /// </summary>
    internal static TimeSpan? LookUpAgainAfter(TimeSpan configured)
    {
        return configured > TimeSpan.Zero ? configured : null;
    }
}
