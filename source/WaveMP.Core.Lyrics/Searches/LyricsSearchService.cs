using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Core.Metadata.Texts;
using WaveMP.Core.Settings.Groups;
using WaveMP.Core.Settings.Snapshots;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Lyrics.Searches;

/// <summary>
/// Searches LRCLIB by title and artist (the title without additions and the main artist of the lookup unless the
/// admin names others), and by the free-text search for both if that finds nothing. Tracks without lyrics are left
/// out. The matches with the searched names come first, then those whose duration fits the track, then those with
/// synchronized lyrics, then the closest duration.
/// </summary>
internal sealed class LyricsSearchService(
    ILyricsRepository repository,
    ILrclibClient lrclib,
    ISettingsSnapshot<LibrarySettings> settings) : ILyricsSearchService
{
    public async Task<LyricsSearchResult> SearchAsync(
        Guid trackId,
        string? title,
        string? artist,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Current.DownloadLyrics)
        {
            return LyricsSearchResult.Disabled;
        }

        LyricsTrackRow? track = await repository.FindTrackAsync(trackId, cancellationToken);
        if (track is null)
        {
            return LyricsSearchResult.TrackNotFound;
        }

        var lookup = LyricsLookupPlan.Of(track);
        string searchedTitle = MetadataText.Normalize(title) ?? lookup.CleanTitle;
        string? searchedArtist = artist is null ? lookup.MainArtist : MetadataText.Normalize(artist);
        var plan = LyricsLookupPlan.Of(searchedTitle, searchedArtist, track.AlbumTitle, track.Duration);

        LrclibResponse response = await lrclib.SearchAsync(
            new LrclibQuery(searchedTitle, searchedArtist, null, null),
            cancellationToken);
        if (response.Status == LrclibStatus.NotFound)
        {
            response = await lrclib.SearchTextAsync(plan.Text, cancellationToken);
        }

        return response.Status switch
        {
            LrclibStatus.RateLimited => new LyricsSearchResult(
                LyricsSearchStatus.RateLimited,
                RetryAfter: response.RetryAfter),
            LrclibStatus.Unavailable => LyricsSearchResult.Unavailable,
            _ => new LyricsSearchResult(
                LyricsSearchStatus.Searched,
                searchedTitle,
                searchedArtist,
                Matches(response.Records, plan)),
        };
    }

    /// <summary>
    /// The tracks with lyrics in their order of preference.
    /// </summary>
    internal static IReadOnlyList<LyricsMatch> Matches(IReadOnlyList<LrclibRecord> records, LyricsLookupPlan plan)
    {
        return [.. records
            .Where(record => record.HasLyrics && !record.Instrumental)
            .DistinctBy(record => record.Id)
            .OrderByDescending(plan.Names.Matches)
            .ThenByDescending(record => LrclibMatcher.FitsDuration(record, plan.Duration))
            .ThenByDescending(record => record.HasSyncedLyrics)
            .ThenBy(record => Difference(record, plan.Duration))
            .Select(record => new LyricsMatch(
                record.Id,
                record.TrackName ?? string.Empty,
                MetadataText.Normalize(record.ArtistName),
                MetadataText.Normalize(record.AlbumName),
                record.Duration is { } seconds ? TimeSpan.FromSeconds(seconds) : null,
                record.HasSyncedLyrics,
                LrclibMatcher.FitsDuration(record, plan.Duration)))];
    }

    private static double Difference(LrclibRecord record, TimeSpan? duration)
    {
        return record.Duration is { } seconds && duration is { } known
            ? Math.Abs(seconds - known.TotalSeconds)
            : double.MaxValue;
    }
}
