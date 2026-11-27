using WaveMP.Core.Lyrics.Saving;
using WaveMP.Core.Lyrics.Services;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Lyrics.Downloads;

/// <summary>
/// Looks up the lyrics of a track in LRCLIB along its <see cref="LyricsLookupPlan"/>: first exactly by title, artist,
/// album and duration, then by a search for title and artist and for the title without additions and the main artist,
/// and if that finds no track with the names of the track, by the free-text search; synchronized lyrics are preferred
/// (see <see cref="LrclibMatcher"/>). If no track fits the duration, the plain lyrics of another version of the song
/// (same title and artist) are kept and marked. Found lyrics are saved by the <see cref="LyricsSaver"/>; an existing
/// lyrics file is never replaced by the lookup, only by lyrics an admin chose.
/// </summary>
internal sealed class LyricsDownloader(
    ILyricsRepository repository,
    ILyricsService lyrics,
    ILrclibClient lrclib,
    LyricsSaver saver,
    ISettingsSnapshot<LibrarySettings> settings) : ILyricsDownloader
{
    public async Task<LyricsDownloadResult> DownloadAsync(
        Guid trackId,
        LyricsDownloadMode mode,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Current.DownloadLyrics)
        {
            return new LyricsDownloadResult(LyricsDownloadOutcome.Disabled);
        }

        LyricsTrackRow? track = await repository.FindTrackAsync(trackId, cancellationToken);
        if (track is null)
        {
            return new LyricsDownloadResult(LyricsDownloadOutcome.TrackNotFound);
        }

        if (track.LyricsFilePath is not null)
        {
            return new LyricsDownloadResult(LyricsDownloadOutcome.LyricsFileExists);
        }

        if (mode == LyricsDownloadMode.Automatic
            && (await lyrics.GetAsync(trackId, cancellationToken)).Status == LyricsStatus.Found)
        {
            return await SaveResultAsync(track, LyricsDownloadStatus.NotNeeded, LyricsDownloadOutcome.NotNeeded);
        }

        LookupResult lookup = await LookUpAsync(LyricsLookupPlan.Of(track), cancellationToken);
        if (lookup.Response.Status is LrclibStatus.RateLimited)
        {
            return new LyricsDownloadResult(LyricsDownloadOutcome.RateLimited, lookup.Response.RetryAfter);
        }

        if (lookup.Response.Status is LrclibStatus.Unavailable)
        {
            return new LyricsDownloadResult(LyricsDownloadOutcome.Unavailable);
        }

        if (lookup.Record is not { } record
            || LrclibMatcher.LyricsOf(record, lookup.OtherVersion) is not { } text)
        {
            return lookup.Instrumental
                ? await SaveResultAsync(track, LyricsDownloadStatus.Instrumental, LyricsDownloadOutcome.Instrumental)
                : await SaveResultAsync(track, LyricsDownloadStatus.NotFound, LyricsDownloadOutcome.NotFound);
        }

        return ToResult(
            await saver.SaveAsync(
                track,
                text,
                LyricsOrigin.Download,
                lookup.OtherVersion,
                replace: false,
                cancellationToken),
            lookup.OtherVersion);
    }

    public async Task<LyricsDownloadResult> DownloadMatchAsync(
        Guid trackId,
        long matchId,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Current.DownloadLyrics)
        {
            return new LyricsDownloadResult(LyricsDownloadOutcome.Disabled);
        }

        LyricsTrackRow? track = await repository.FindTrackAsync(trackId, cancellationToken);
        if (track is null)
        {
            return new LyricsDownloadResult(LyricsDownloadOutcome.TrackNotFound);
        }

        LrclibResponse response = await lrclib.GetByIdAsync(matchId, cancellationToken);
        switch (response.Status)
        {
            case LrclibStatus.RateLimited:
                return new LyricsDownloadResult(LyricsDownloadOutcome.RateLimited, response.RetryAfter);
            case LrclibStatus.Unavailable:
                return new LyricsDownloadResult(LyricsDownloadOutcome.Unavailable);
        }

        if (response.Records.FirstOrDefault() is not { } record)
        {
            return new LyricsDownloadResult(LyricsDownloadOutcome.NotFound);
        }

        bool otherVersion = !LrclibMatcher.FitsDuration(record, track.Duration);
        if (record.Instrumental || LrclibMatcher.LyricsOf(record, otherVersion) is not { } text)
        {
            return new LyricsDownloadResult(record.Instrumental
                ? LyricsDownloadOutcome.Instrumental
                : LyricsDownloadOutcome.NotFound);
        }

        return ToResult(
            await saver.SaveAsync(track, text, LyricsOrigin.Download, otherVersion, replace: true, cancellationToken),
            otherVersion);
    }

    private static LyricsDownloadResult ToResult(LyricsSaveOutcome outcome, bool otherVersion)
    {
        return new LyricsDownloadResult(
            outcome switch
            {
                LyricsSaveOutcome.SavedToFile => LyricsDownloadOutcome.SavedToFile,
                LyricsSaveOutcome.SavedToDatabase => LyricsDownloadOutcome.SavedToDatabase,
                LyricsSaveOutcome.FileExists => LyricsDownloadOutcome.LyricsFileExists,
                LyricsSaveOutcome.FileNotWritable => LyricsDownloadOutcome.FileNotWritable,
                _ => LyricsDownloadOutcome.TrackNotFound,
            })
        {
            OtherVersion = otherVersion
                && outcome is LyricsSaveOutcome.SavedToFile or LyricsSaveOutcome.SavedToDatabase,
        };
    }

    /// <summary>
    /// Asks LRCLIB along the plan. Returns the last answer, the chosen track, whether it belongs to another version
    /// and whether a fitting track is instrumental.
    /// </summary>
    private async Task<LookupResult> LookUpAsync(LyricsLookupPlan plan, CancellationToken cancellationToken)
    {
        List<LrclibRecord> seen = [];
        bool instrumental = false;
        LrclibResponse last = LrclibResponse.NotFound;
        if (plan.Query.IsComplete)
        {
            last = await lrclib.GetAsync(plan.Query, cancellationToken);
            if (Ask(last, plan.Query, seen, ref instrumental) is { } result)
            {
                return result;
            }
        }

        foreach (LrclibQuery query in plan.SearchQueries)
        {
            last = await lrclib.SearchAsync(query, cancellationToken);
            if (Ask(last, query, seen, ref instrumental) is { } result)
            {
                return result;
            }
        }

        // The free-text search finds tracks the search by fields misses; only those with the names of the track count.
        if (!instrumental && !seen.Any(plan.Names.Matches))
        {
            last = await lrclib.SearchTextAsync(plan.Text, cancellationToken);
            LrclibResponse named = last.Status == LrclibStatus.Found
                ? LrclibResponse.Found([.. last.Records.Where(plan.Names.Matches)])
                : last;
            if (Ask(named, plan.Query, seen, ref instrumental) is { } result)
            {
                return result;
            }
        }

        if (instrumental)
        {
            return new LookupResult(last, null, OtherVersion: false, Instrumental: true);
        }

        LrclibRecord? other = LrclibMatcher.ChooseOtherVersion(seen, plan.Query, plan.Names);
        return new LookupResult(last, other, OtherVersion: other is not null, Instrumental: false);

        LookupResult? Ask(
            LrclibResponse response,
            LrclibQuery query,
            List<LrclibRecord> records,
            ref bool isInstrumental)
        {
            if (response.Status is LrclibStatus.RateLimited or LrclibStatus.Unavailable)
            {
                return new LookupResult(response, null, OtherVersion: false, Instrumental: false);
            }

            records.AddRange(response.Records);
            if (LrclibMatcher.Choose(response.Records, query, plan.Names, out bool fittingInstrumental) is { } match)
            {
                return new LookupResult(response, match, OtherVersion: false, Instrumental: false);
            }

            isInstrumental |= fittingInstrumental;
            return null;
        }
    }

    /// <summary>
    /// Stores the result of the lookup with the track and returns the outcome, or
    /// <see cref="LyricsDownloadOutcome.TrackNotFound"/> if the track is gone in the meantime.
    /// </summary>
    private async Task<LyricsDownloadResult> SaveResultAsync(
        LyricsTrackRow track,
        LyricsDownloadStatus status,
        LyricsDownloadOutcome outcome)
    {
        bool saved = await saver.SaveResultAsync(track, status, null, otherVersion: false, CancellationToken.None);
        return new LyricsDownloadResult(saved ? outcome : LyricsDownloadOutcome.TrackNotFound);
    }

    /// <summary>
    /// The result of asking LRCLIB.
    /// </summary>
    private sealed record LookupResult(
        LrclibResponse Response,
        LrclibRecord? Record,
        bool OtherVersion,
        bool Instrumental);
}
