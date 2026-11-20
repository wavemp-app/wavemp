using WaveMP.Data.Projections;

namespace WaveMP.Core.Recaps.Statistics;

/// <summary>
/// Counts the statistics of a period from its plays. Every play counts with the whole duration of its track (the
/// history knows only that the track played long enough). Rankings put the most plays first, then the longest
/// listening time, then the name without regard to case.
/// </summary>
internal static class ListeningStatisticsCalculator
{
    /// <summary>
    /// How many tracks of a group are kept as candidates of its cover.
    /// </summary>
    public const int CoverCandidates = 5;

    /// <summary>
    /// Periods up to this many days are counted per day.
    /// </summary>
    public const int MaxDaysOfDailyTimeline = 45;

    /// <summary>
    /// Periods up to this many days are counted per week.
    /// </summary>
    public const int MaxDaysOfWeeklyTimeline = 190;

    /// <summary>
    /// Periods up to this many days are counted per month; longer ones per year.
    /// </summary>
    public const int MaxDaysOfMonthlyTimeline = 2200;

    /// <param name="plays">The plays of the period.</param>
    /// <param name="firstArtistPlays">The first play of each played artist over the whole history.</param>
    /// <param name="from">First day of the period.</param>
    /// <param name="to">Last day of the period.</param>
    /// <param name="timeZone">Time zone of the days and hours.</param>
    /// <param name="top">Length of the rankings.</param>
    public static StatisticsCalculation Calculate(
        IReadOnlyList<PlayStatisticsRow> plays,
        IReadOnlyDictionary<Guid, DateTimeOffset> firstArtistPlays,
        DateOnly from,
        DateOnly to,
        TimeZoneInfo timeZone,
        int top)
    {
        DateTimeOffset periodStart = StatisticsTimeZones.StartOfDay(from, timeZone);
        List<CountedTrack> tracks = CountTracks(plays);
        var trackById = tracks.ToDictionary(track => track.TrackId);

        List<CountedGroup> artists = CountGroups(
            plays.Where(play => play.ArtistId is not null),
            play => play.ArtistId!.Value.ToString(),
            group => group.First().ArtistName ?? string.Empty,
            trackById,
            group => group.First().ArtistId!.Value);
        List<CountedGroup> albums = CountGroups(
            plays.Where(play => play.AlbumId is not null),
            play => play.AlbumId!.Value.ToString(),
            group => group.First().AlbumTitle ?? string.Empty,
            trackById,
            group => group.First().AlbumId!.Value,
            group => (group.First().AlbumArtistId, group.First().AlbumArtistName));
        List<CountedGroup> genres = CountGroups(
            plays.Where(play => !string.IsNullOrWhiteSpace(play.Genre)),
            play => play.Genre!.Trim().ToUpperInvariant(),
            MostPlayedSpelling,
            trackById,
            _ => Guid.Empty);

        List<CountedGroup> newArtists = [.. artists
            .Where(artist => firstArtistPlays.TryGetValue(artist.Id, out DateTimeOffset first) && first >= periodStart)
            .Select(artist => artist with { FirstPlayedAt = firstArtistPlays[artist.Id] })
            .OrderByDescending(artist => artist.Plays)
            .ThenBy(artist => artist.FirstPlayedAt)
            .ThenBy(artist => artist.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(artist => artist.Id)];

        StatisticsTotals totals = new(
            plays.Count,
            Sum(plays),
            tracks.Count,
            artists.Count,
            albums.Count,
            genres.Count,
            newArtists.Count,
            plays.Select(play => StatisticsTimeZones.DayOf(play.PlayedAt, timeZone)).Distinct().Count());

        return new StatisticsCalculation(
            totals,
            [.. tracks.Take(top)],
            [.. artists.Take(top)],
            [.. albums.Take(top)],
            [.. genres.Take(top)],
            [.. newArtists.Take(top)],
            BuildTimeline(plays, from, to, timeZone),
            CountHours(plays, timeZone));
    }

    /// <summary>
    /// The granularity of the timeline of a period from <paramref name="from"/> to <paramref name="to"/>.
    /// </summary>
    public static TimelineGranularity GranularityOf(DateOnly from, DateOnly to)
    {
        int days = to.DayNumber - from.DayNumber + 1;
        return days switch
        {
            <= MaxDaysOfDailyTimeline => TimelineGranularity.Day,
            <= MaxDaysOfWeeklyTimeline => TimelineGranularity.Week,
            <= MaxDaysOfMonthlyTimeline => TimelineGranularity.Month,
            _ => TimelineGranularity.Year,
        };
    }

    /// <summary>
    /// The first day of the day, week (Monday), month or year that contains <paramref name="day"/>.
    /// </summary>
    public static DateOnly StartOfStep(DateOnly day, TimelineGranularity granularity)
    {
        return granularity switch
        {
            TimelineGranularity.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
            TimelineGranularity.Month => new DateOnly(day.Year, day.Month, 1),
            TimelineGranularity.Year => new DateOnly(day.Year, 1, 1),
            _ => day,
        };
    }

    private static DateOnly NextStep(DateOnly start, TimelineGranularity granularity)
    {
        return granularity switch
        {
            TimelineGranularity.Week => start.AddDays(7),
            TimelineGranularity.Month => start.AddMonths(1),
            TimelineGranularity.Year => start.AddYears(1),
            _ => start.AddDays(1),
        };
    }

    private static List<CountedTrack> CountTracks(IReadOnlyList<PlayStatisticsRow> plays)
    {
        return [.. plays
            .GroupBy(play => play.TrackId)
            .Select(group => new
            {
                Track = new CountedTrack(group.Key, group.Count(), Sum(group)),
                group.First().Title,
            })
            .OrderByDescending(item => item.Track.Plays)
            .ThenByDescending(item => item.Track.ListeningTime)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Track.TrackId)
            .Select(item => item.Track)];
    }

    private static List<CountedGroup> CountGroups(
        IEnumerable<PlayStatisticsRow> plays,
        Func<PlayStatisticsRow, string> keyOf,
        Func<IGrouping<string, PlayStatisticsRow>, string> nameOf,
        Dictionary<Guid, CountedTrack> trackById,
        Func<IGrouping<string, PlayStatisticsRow>, Guid> idOf,
        Func<IGrouping<string, PlayStatisticsRow>, (Guid? Id, string? Name)>? artistOf = null)
    {
        return [.. plays
            .GroupBy(keyOf, StringComparer.Ordinal)
            .Select(group =>
            {
                (Guid? artistId, string? artistName) = artistOf?.Invoke(group) ?? (null, null);
                List<Guid> trackIds = [.. group.Select(play => play.TrackId).Distinct()];
                return new CountedGroup(
                    idOf(group),
                    nameOf(group),
                    group.Count(),
                    Sum(group),
                    trackIds.Count,
                    [.. trackIds
                        .Select(id => trackById[id])
                        .OrderByDescending(track => track.Plays)
                        .ThenByDescending(track => track.ListeningTime)
                        .ThenBy(track => track.TrackId)
                        .Take(CoverCandidates)
                        .Select(track => track.TrackId)],
                    artistId,
                    artistName);
            })
            .OrderByDescending(group => group.Plays)
            .ThenByDescending(group => group.ListeningTime)
            .ThenBy(group => group.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.Id)];
    }

    /// <summary>
    /// The spelling of a genre played most often; ties by ordinal order.
    /// </summary>
    private static string MostPlayedSpelling(IGrouping<string, PlayStatisticsRow> group)
    {
        return group
            .Select(play => play.Genre!.Trim())
            .GroupBy(spelling => spelling, StringComparer.Ordinal)
            .OrderByDescending(spelling => spelling.Count())
            .ThenBy(spelling => spelling.Key, StringComparer.Ordinal)
            .First()
            .Key;
    }

    private static Timeline BuildTimeline(
        IReadOnlyList<PlayStatisticsRow> plays,
        DateOnly from,
        DateOnly to,
        TimeZoneInfo timeZone)
    {
        TimelineGranularity granularity = GranularityOf(from, to);
        Dictionary<DateOnly, (int Plays, TimeSpan Time)> counted = [];
        foreach (PlayStatisticsRow play in plays)
        {
            DateOnly step = StartOfStep(StatisticsTimeZones.DayOf(play.PlayedAt, timeZone), granularity);
            (int count, TimeSpan time) = counted.GetValueOrDefault(step);
            counted[step] = (count + 1, time + (play.Duration ?? TimeSpan.Zero));
        }

        List<TimelineBucket> buckets = [];
        DateOnly last = StartOfStep(to, granularity);
        for (DateOnly start = StartOfStep(from, granularity); start <= last; start = NextStep(start, granularity))
        {
            (int count, TimeSpan time) = counted.GetValueOrDefault(start);
            buckets.Add(new TimelineBucket(start, count, time));
        }

        return new Timeline(granularity, buckets);
    }

    private static List<HourStatistics> CountHours(IReadOnlyList<PlayStatisticsRow> plays, TimeZoneInfo timeZone)
    {
        int[] counts = new int[24];
        var times = new TimeSpan[24];
        foreach (PlayStatisticsRow play in plays)
        {
            int hour = TimeZoneInfo.ConvertTime(play.PlayedAt, timeZone).Hour;
            counts[hour]++;
            times[hour] += play.Duration ?? TimeSpan.Zero;
        }

        return [.. Enumerable.Range(0, 24).Select(hour => new HourStatistics(hour, counts[hour], times[hour]))];
    }

    private static TimeSpan Sum(IEnumerable<PlayStatisticsRow> plays)
    {
        return plays.Aggregate(TimeSpan.Zero, (sum, play) => sum + (play.Duration ?? TimeSpan.Zero));
    }
}
