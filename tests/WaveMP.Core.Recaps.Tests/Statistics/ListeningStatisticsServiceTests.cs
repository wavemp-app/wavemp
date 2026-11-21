using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Recaps.Statistics;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Recaps.Tests.Statistics;

[TestFixture]
public class ListeningStatisticsServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid _userId = Guid.Parse("00000000-0000-0000-0000-0000000000f1");
    private static readonly Guid _viewerId = Guid.Parse("00000000-0000-0000-0000-0000000000f2");

    private IPlayHistoryRepository _history = null!;
    private ITrackRepository _tracks = null!;
    private ListeningStatisticsService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _history = Substitute.For<IPlayHistoryRepository>();
        _history.ListPlaysAsync(default, default, default, default).ReturnsForAnyArgs([]);
        _history.FindFirstPlaysOfArtistsAsync(default, default!, default)
            .ReturnsForAnyArgs(new Dictionary<Guid, DateTimeOffset>());
        _tracks = Substitute.For<ITrackRepository>();
        _tracks.ListByIdsAsync(default!, default, default).ReturnsForAnyArgs([]);
        TimeProvider clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(_now);
        _service = new ListeningStatisticsService(_history, _tracks, clock);
    }

    [Test]
    public async Task GetAsync_WithDays_ReadsThePlaysFromMidnightToMidnightInTheTimeZone()
    {
        var berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

        ListeningStatistics result =
            await _service.GetAsync(Query(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 2), berlin));

        await _history.Received(1).ListPlaysAsync(
            _userId,
            new DateTimeOffset(2026, 10, 31, 23, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 2, 23, 0, 0, TimeSpan.Zero),
            Arg.Any<CancellationToken>());
        result.From.Should().Be(new DateOnly(2026, 11, 1));
        result.To.Should().Be(new DateOnly(2026, 11, 2));
        result.TimeZone.Should().Be(berlin.Id);
    }

    [Test]
    public async Task GetAsync_WithoutDays_CountsFromTheFirstPlayUpToToday()
    {
        _history.FindFirstPlayAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new DateTimeOffset(2025, 3, 4, 10, 0, 0, TimeSpan.Zero));

        ListeningStatistics result = await _service.GetAsync(Query(null, null, TimeZoneInfo.Utc));

        result.From.Should().Be(new DateOnly(2025, 3, 4));
        result.To.Should().Be(new DateOnly(2026, 11, 20));
    }

    [Test]
    public async Task GetAsync_WithoutDaysAndPlays_CountsToday()
    {
        _history.FindFirstPlayAsync(_userId, Arg.Any<CancellationToken>()).Returns((DateTimeOffset?)null);

        ListeningStatistics result = await _service.GetAsync(Query(null, null, TimeZoneInfo.Utc));

        result.From.Should().Be(new DateOnly(2026, 11, 20));
        result.To.Should().Be(new DateOnly(2026, 11, 20));
        result.Timeline.Buckets.Should().ContainSingle();
    }

    [Test]
    public async Task GetAsync_FirstPlayAfterTheLastDay_StartsOnTheLastDay()
    {
        _history.FindFirstPlayAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new DateTimeOffset(2026, 11, 15, 10, 0, 0, TimeSpan.Zero));

        ListeningStatistics result = await _service.GetAsync(Query(null, new DateOnly(2026, 11, 1), TimeZoneInfo.Utc));

        result.From.Should().Be(new DateOnly(2026, 11, 1));
        result.To.Should().Be(new DateOnly(2026, 11, 1));
    }

    [Test]
    public async Task GetAsync_Always_ShowsTheTracksAsTheViewerSeesThemAndLeavesOutRemovedTracks()
    {
        DateTimeOffset day = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        _history.ListPlaysAsync(default, default, default, default).ReturnsForAnyArgs(
        [
            Plays.Of(Plays.Ballad, day),
            Plays.Of(Plays.Ballad, day.AddHours(1)),
            Plays.Of(Plays.Opening, day.AddHours(2)),
        ]);
        TrackRow opening = Plays.Row(Plays.Opening, hasCover: false);
        _tracks.ListByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _viewerId, Arg.Any<CancellationToken>())
            .Returns([opening]);

        ListeningStatistics result = await _service.GetAsync(
            Query(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), TimeZoneInfo.Utc));

        result.TopTracks.Should().ContainSingle().Which.Should().Be(new TopTrack(opening, 1, TimeSpan.FromMinutes(3)));
        result.Totals.Plays.Should().Be(3);
    }

    [Test]
    public async Task GetAsync_Always_TakesTheMostPlayedTrackWithACoverAsCoverOfAGroup()
    {
        DateTimeOffset day = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        _history.ListPlaysAsync(default, default, default, default).ReturnsForAnyArgs(
        [
            Plays.Of(Plays.Ballad, day),
            Plays.Of(Plays.Ballad, day.AddHours(1)),
            Plays.Of(Plays.Opening, day.AddHours(2)),
            Plays.Of(Plays.Closing, day.AddHours(3)),
        ]);
        _tracks.ListByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _viewerId, Arg.Any<CancellationToken>())
            .Returns(
            [
                Plays.Row(Plays.Ballad, hasCover: false),
                Plays.Row(Plays.Opening, hasCover: true),
                Plays.Row(Plays.Closing, hasCover: false),
            ]);

        ListeningStatistics result = await _service.GetAsync(
            Query(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), TimeZoneInfo.Utc));

        result.TopArtists.Select(artist => (artist.Id, artist.CoverTrackId)).Should().Equal(
            (Plays.Aurora, Plays.Opening),
            (Plays.Breeze, (Guid?)null));
        result.TopAlbums[0].CoverTrackId.Should().Be(Plays.Opening);
        result.TopGenres[0].CoverTrackId.Should().Be(Plays.Opening);
    }

    [Test]
    public async Task GetAsync_Always_MarksTopAlbumsAsCompilationsAsTheirTracksNameThem()
    {
        DateTimeOffset day = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        _history.ListPlaysAsync(default, default, default, default).ReturnsForAnyArgs(
        [
            Plays.Of(Plays.Ballad, day),
            Plays.Of(Plays.Opening, day.AddHours(1)),
            Plays.Of(Plays.Closing, day.AddHours(2)),
        ]);
        _tracks.ListByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _viewerId, Arg.Any<CancellationToken>())
            .Returns(
            [
                Plays.Row(Plays.Ballad, hasCover: false, Plays.Daybreak, albumIsCompilation: true),
                Plays.Row(Plays.Opening, hasCover: false, Plays.Daybreak, albumIsCompilation: true),
                // Moved to another album, which is a compilation, since it was played.
                Plays.Row(Plays.Closing, hasCover: false, Guid.NewGuid(), albumIsCompilation: true),
            ]);

        ListeningStatistics result = await _service.GetAsync(
            Query(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), TimeZoneInfo.Utc));

        result.TopAlbums.Select(album => (album.Id, album.IsCompilation)).Should().Equal(
            (Plays.Daybreak, true),
            (Plays.Evening, false));
    }

    [Test]
    public async Task GetAsync_Always_AsksForTheFirstPlaysOfThePlayedArtists()
    {
        DateTimeOffset day = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);
        _history.ListPlaysAsync(default, default, default, default).ReturnsForAnyArgs(
            [Plays.Of(Plays.Ballad, day), Plays.Of(Plays.Closing, day), Plays.Of(Plays.Single, day)]);
        _history.FindFirstPlaysOfArtistsAsync(default, default!, default)
            .ReturnsForAnyArgs(new Dictionary<Guid, DateTimeOffset> { [Plays.Breeze] = day });

        ListeningStatistics result = await _service.GetAsync(
            Query(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), TimeZoneInfo.Utc));

        await _history.Received(1).FindFirstPlaysOfArtistsAsync(
            _userId,
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Order().SequenceEqual(new[] { Plays.Aurora, Plays.Breeze })),
            Arg.Any<CancellationToken>());
        result.NewArtists.Should().ContainSingle().Which.Id.Should().Be(Plays.Breeze);
    }

    private static StatisticsQuery Query(DateOnly? from, DateOnly? to, TimeZoneInfo timeZone)
    {
        return new StatisticsQuery(_userId, from, to, timeZone, 10, _viewerId);
    }
}
