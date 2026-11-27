using AwesomeAssertions;
using WaveMP.Core.Recaps.Statistics;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;

namespace WaveMP.Core.Recaps.Tests.Statistics;

[TestFixture]
public class ListeningStatisticsCalculatorTests
{
    private static readonly DateOnly _from = new(2026, 11, 1);
    private static readonly DateOnly _to = new(2026, 11, 30);
    private static readonly DateTimeOffset _day = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void Calculate_Always_RanksTracksByPlaysThenListeningTime()
    {
        List<PlayStatisticsRow> plays =
        [
            Plays.Of(Plays.Opening, _day),
            Plays.Of(Plays.Ballad, _day.AddHours(1)),
            Plays.Of(Plays.Closing, _day.AddHours(2)),
            Plays.Of(Plays.Closing, _day.AddHours(3)),
            Plays.Of(Plays.Single, _day.AddHours(4)),
        ];

        StatisticsCalculation result = Calculate(plays);

        result.TopTracks.Select(track => track.TrackId).Should().Equal(
            Plays.Closing,
            Plays.Ballad,
            Plays.Opening,
            Plays.Single);
        result.TopTracks[0].Should().Be(new CountedTrack(Plays.Closing, 2, TimeSpan.FromMinutes(10)));
    }

    [Test]
    public void Calculate_Always_CountsTheTotalsOfThePeriod()
    {
        List<PlayStatisticsRow> plays =
        [
            Plays.Of(Plays.Opening, _day),
            Plays.Of(Plays.Ballad, _day.AddHours(1)),
            Plays.Of(Plays.Ballad, _day.AddDays(2)),
            Plays.Of(Plays.Closing, _day.AddDays(3)),
            Plays.Of(Plays.Single, _day.AddDays(3)),
        ];

        StatisticsCalculation result = Calculate(plays);

        result.Totals.Should().Be(new StatisticsTotals(
            Plays: 5,
            ListeningTime: TimeSpan.FromMinutes(3 + 4 + 4 + 5 + 2),
            Tracks: 4,
            Artists: 2,
            Albums: 2,
            Genres: 2,
            NewArtists: 0,
            ActiveDays: 3));
    }

    [Test]
    public void Calculate_TracksOfOneGenre_CountsThemByTheIdOfTheGenre()
    {
        List<PlayStatisticsRow> plays =
        [
            Plays.Of(Plays.Opening, _day),
            Plays.Of(Plays.Opening, _day.AddHours(1)),
            Plays.Of(Plays.Ballad, _day.AddHours(2)),
            Plays.Of(Plays.Closing, _day.AddHours(3)),
        ];

        StatisticsCalculation result = Calculate(plays);

        result.TopGenres.Select(genre => (genre.Name, genre.Plays, genre.Tracks)).Should().Equal(
            ("Pop", 3, 2),
            ("Rock", 1, 1));
    }

    [Test]
    public void Calculate_SongWithSeveralArtists_CountsForEachOfThem()
    {
        PlayStatisticsRow duet = Plays.Of(Plays.Ballad, _day) with
        {
            Artists =
            [
                new ArtistCreditRow(Plays.Aurora, "Aurora"),
                new ArtistCreditRow(Plays.Breeze, "Breeze", ArtistRole.Featured),
            ],
        };
        List<PlayStatisticsRow> plays =
            [duet, duet with { PlayedAt = _day.AddHours(1) }, Plays.Of(Plays.Closing, _day)];

        StatisticsCalculation result = Calculate(plays);

        result.TopArtists.Select(artist => (artist.Name, artist.Plays, artist.Tracks)).Should().Equal(
            ("Breeze", 3, 2),
            ("Aurora", 2, 1));
        result.Totals.Artists.Should().Be(2);
        result.Totals.Plays.Should().Be(3);
    }

    [Test]
    public void Calculate_TrackWithoutArtistAlbumAndGenre_CountsOnlyAsTrack()
    {
        List<PlayStatisticsRow> plays = [Plays.Of(Plays.Single, _day)];

        StatisticsCalculation result = Calculate(plays);

        result.TopTracks.Should().ContainSingle();
        result.TopArtists.Should().BeEmpty();
        result.TopAlbums.Should().BeEmpty();
        result.TopGenres.Should().BeEmpty();
        result.NewArtists.Should().BeEmpty();
    }

    [Test]
    public void Calculate_Albums_NameTheirArtistAndTheirMostPlayedTracksFirst()
    {
        List<PlayStatisticsRow> plays =
        [
            Plays.Of(Plays.Opening, _day),
            Plays.Of(Plays.Ballad, _day.AddHours(1)),
            Plays.Of(Plays.Ballad, _day.AddHours(2)),
        ];

        StatisticsCalculation result = Calculate(plays);

        CountedGroup album = result.TopAlbums.Should().ContainSingle().Subject;
        album.Id.Should().Be(Plays.Daybreak);
        album.Name.Should().Be("Daybreak");
        album.ArtistId.Should().Be(Plays.Aurora);
        album.ArtistName.Should().Be("Aurora");
        album.Plays.Should().Be(3);
        album.Tracks.Should().Be(2);
        album.TrackIds.Should().Equal(Plays.Ballad, Plays.Opening);
    }

    [Test]
    public void Calculate_ArtistsFirstPlayedInThePeriod_AreNewArtists()
    {
        List<PlayStatisticsRow> plays =
        [
            Plays.Of(Plays.Ballad, _day),
            Plays.Of(Plays.Closing, _day.AddHours(1)),
            Plays.Of(Plays.Closing, _day.AddHours(2)),
        ];
        Dictionary<Guid, DateTimeOffset> first = new()
        {
            [Plays.Aurora] = new DateTimeOffset(2026, 10, 31, 23, 0, 0, TimeSpan.Zero),
            [Plays.Breeze] = _day.AddHours(1),
        };

        StatisticsCalculation result = ListeningStatisticsCalculator.Calculate(
            plays,
            first,
            _from,
            _to,
            TimeZoneInfo.Utc,
            10);

        CountedGroup artist = result.NewArtists.Should().ContainSingle().Subject;
        artist.Id.Should().Be(Plays.Breeze);
        artist.FirstPlayedAt.Should().Be(_day.AddHours(1));
        artist.Plays.Should().Be(2);
        result.Totals.NewArtists.Should().Be(1);
    }

    [Test]
    public void Calculate_FirstPlayBeforeThePeriodInItsTimeZone_IsNoNewArtist()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        // 23:30 UTC on 31 October is 00:30 on 1 November in Berlin: in the period.
        DateTimeOffset firstPlay = new(2026, 10, 31, 23, 30, 0, TimeSpan.Zero);
        Dictionary<Guid, DateTimeOffset> first = new()
        {
            [Plays.Aurora] = firstPlay,
            [Plays.Breeze] = new DateTimeOffset(2026, 10, 31, 22, 30, 0, TimeSpan.Zero),
        };

        StatisticsCalculation result = ListeningStatisticsCalculator.Calculate(
            [Plays.Of(Plays.Ballad, firstPlay), Plays.Of(Plays.Closing, _day)],
            first,
            _from,
            _to,
            berlin,
            10);

        result.NewArtists.Select(artist => artist.Id).Should().Equal(Plays.Aurora);
    }

    [Test]
    public void Calculate_WithTop_CutsTheRankingsButNotTheTotals()
    {
        List<PlayStatisticsRow> plays =
        [
            Plays.Of(Plays.Opening, _day),
            Plays.Of(Plays.Ballad, _day.AddHours(1)),
            Plays.Of(Plays.Closing, _day.AddHours(2)),
        ];

        StatisticsCalculation result = ListeningStatisticsCalculator.Calculate(
            plays,
            new Dictionary<Guid, DateTimeOffset>(),
            _from,
            _to,
            TimeZoneInfo.Utc,
            1);

        result.TopTracks.Should().ContainSingle();
        result.TopArtists.Should().ContainSingle().Which.Id.Should().Be(Plays.Aurora);
        result.TopGenres.Should().ContainSingle();
        result.Totals.Tracks.Should().Be(3);
    }

    [Test]
    public void Calculate_Always_CountsDaysAndHoursInTheTimeZone()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
        // 23:30 UTC on 9 November is 00:30 on 10 November in Berlin.
        DateTimeOffset late = new(2026, 11, 9, 23, 30, 0, TimeSpan.Zero);

        StatisticsCalculation result = ListeningStatisticsCalculator.Calculate(
            [Plays.Of(Plays.Ballad, late)],
            new Dictionary<Guid, DateTimeOffset>(),
            _from,
            _to,
            berlin,
            10);

        result.Hours.Should().HaveCount(24);
        result.Hours.Single(hour => hour.Plays > 0).Should().Be(new HourStatistics(0, 1, TimeSpan.FromMinutes(4)));
        result.Timeline.Buckets.Single(bucket => bucket.Plays > 0).Start.Should().Be(new DateOnly(2026, 11, 10));
    }

    [Test]
    public void Calculate_Always_FillsTheTimelineWithoutGaps()
    {
        StatisticsCalculation result = ListeningStatisticsCalculator.Calculate(
            [Plays.Of(Plays.Ballad, _day), Plays.Of(Plays.Opening, _day.AddHours(1))],
            new Dictionary<Guid, DateTimeOffset>(),
            new DateOnly(2026, 11, 8),
            new DateOnly(2026, 11, 12),
            TimeZoneInfo.Utc,
            10);

        result.Timeline.Granularity.Should().Be(TimelineGranularity.Day);
        result.Timeline.Buckets.Should().Equal(
            new TimelineBucket(new DateOnly(2026, 11, 8), 0, TimeSpan.Zero),
            new TimelineBucket(new DateOnly(2026, 11, 9), 0, TimeSpan.Zero),
            new TimelineBucket(new DateOnly(2026, 11, 10), 2, TimeSpan.FromMinutes(7)),
            new TimelineBucket(new DateOnly(2026, 11, 11), 0, TimeSpan.Zero),
            new TimelineBucket(new DateOnly(2026, 11, 12), 0, TimeSpan.Zero));
    }

    [Test]
    public void Calculate_LongPeriod_CountsTheTimelinePerMonth()
    {
        StatisticsCalculation result = ListeningStatisticsCalculator.Calculate(
            [Plays.Of(Plays.Ballad, _day)],
            new Dictionary<Guid, DateTimeOffset>(),
            new DateOnly(2025, 11, 21),
            new DateOnly(2026, 11, 20),
            TimeZoneInfo.Utc,
            10);

        result.Timeline.Granularity.Should().Be(TimelineGranularity.Month);
        result.Timeline.Buckets.Should().HaveCount(13);
        result.Timeline.Buckets[0].Start.Should().Be(new DateOnly(2025, 11, 1));
        result.Timeline.Buckets[^1].Should().Be(
            new TimelineBucket(new DateOnly(2026, 11, 1), 1, TimeSpan.FromMinutes(4)));
    }

    [Test]
    public void Calculate_WithoutPlays_ReturnsEmptyRankingsAndZeroTotals()
    {
        StatisticsCalculation result = Calculate([]);

        result.Totals.Should().Be(new StatisticsTotals(0, TimeSpan.Zero, 0, 0, 0, 0, 0, 0));
        result.TopTracks.Should().BeEmpty();
        result.Timeline.Buckets.Should().HaveCount(30).And.OnlyContain(bucket => bucket.Plays == 0);
        result.Hours.Should().HaveCount(24).And.OnlyContain(hour => hour.Plays == 0);
    }

    [Test]
    public void Calculate_GroupWithManyTracks_KeepsItsFiveMostPlayedAsCoverCandidates()
    {
        List<PlayStatisticsRow> plays = [];
        for (int index = 1; index <= 7; index++)
        {
            var track = Guid.Parse($"00000000-0000-0000-0000-0000000001{index:00}");
            for (int play = 0; play < index; play++)
            {
                plays.Add(Plays.Of(Plays.Ballad, _day) with { TrackId = track });
            }
        }

        StatisticsCalculation result = Calculate(plays);

        CountedGroup artist = result.TopArtists.Should().ContainSingle().Subject;
        artist.Tracks.Should().Be(7);
        artist.TrackIds.Should().HaveCount(ListeningStatisticsCalculator.CoverCandidates);
        artist.TrackIds[0].Should().Be(Guid.Parse("00000000-0000-0000-0000-000000000107"));
    }

    [TestCase("2026-11-01", "2026-11-30", TimelineGranularity.Day)]
    [TestCase("2026-10-01", "2026-11-14", TimelineGranularity.Day)]
    [TestCase("2026-08-21", "2026-11-20", TimelineGranularity.Week)]
    [TestCase("2025-11-21", "2026-11-20", TimelineGranularity.Month)]
    [TestCase("2020-01-01", "2026-11-20", TimelineGranularity.Year)]
    public void GranularityOf_Always_FollowsTheLengthOfThePeriod(
        string from,
        string to,
        TimelineGranularity expected)
    {
        ListeningStatisticsCalculator.GranularityOf(DateOnly.Parse(from), DateOnly.Parse(to))
            .Should().Be(expected);
    }

    [TestCase("2026-11-20", TimelineGranularity.Day, "2026-11-20")]
    [TestCase("2026-11-20", TimelineGranularity.Week, "2026-11-16")]
    [TestCase("2026-11-22", TimelineGranularity.Week, "2026-11-16")]
    [TestCase("2026-11-16", TimelineGranularity.Week, "2026-11-16")]
    [TestCase("2026-11-20", TimelineGranularity.Month, "2026-11-01")]
    [TestCase("2026-11-20", TimelineGranularity.Year, "2026-01-01")]
    public void StartOfStep_Always_ReturnsTheFirstDayOfTheStep(
        string day,
        TimelineGranularity granularity,
        string expected)
    {
        ListeningStatisticsCalculator.StartOfStep(DateOnly.Parse(day), granularity)
            .Should().Be(DateOnly.Parse(expected));
    }

    private static StatisticsCalculation Calculate(IReadOnlyList<PlayStatisticsRow> plays)
    {
        return ListeningStatisticsCalculator.Calculate(
            plays,
            new Dictionary<Guid, DateTimeOffset>(),
            _from,
            _to,
            TimeZoneInfo.Utc,
            10);
    }
}
