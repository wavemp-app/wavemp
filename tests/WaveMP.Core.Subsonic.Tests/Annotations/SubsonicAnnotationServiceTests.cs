using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Collection.Favorites;
using WaveMP.Core.Collection.Ratings;
using WaveMP.Core.Scrobbling.Plays;
using WaveMP.Core.Streaming.Histories;
using WaveMP.Core.Subsonic.Annotations;
using WaveMP.Core.Subsonic.Authentication;
using WaveMP.Core.Subsonic.NowPlaying;
using WaveMP.Core.Subsonic.Requests;
using WaveMP.Core.Subsonic.Responses;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Subsonic.Tests.Annotations;

[TestFixture]
public class SubsonicAnnotationServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly SubsonicUser _user = new(Guid.NewGuid(), "Anna");
    private readonly Guid _trackId = Guid.NewGuid();

    private IFavoriteService _favorites = null!;
    private IRatingService _ratings = null!;
    private IPlayHistoryService _history = null!;
    private INowPlayingService _nowPlaying = null!;
    private ISubsonicNowPlayingRegistry _registry = null!;
    private SubsonicAnnotationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _favorites = Substitute.For<IFavoriteService>();
        _favorites.AddAsync(_user.Id, Arg.Any<FavoriteType>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(FavoriteStatus.NotFound);
        _ratings = Substitute.For<IRatingService>();
        _history = Substitute.For<IPlayHistoryService>();
        _history.RecordAsync(_user.Id, Arg.Any<Guid>(), Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(new PlayHistoryResult(PlayHistoryStatus.TrackNotFound, null));
        _history.RecordAsync(_user.Id, _trackId, Arg.Any<DateTimeOffset?>(), Arg.Any<CancellationToken>())
            .Returns(new PlayHistoryResult(PlayHistoryStatus.Created, null));
        _nowPlaying = Substitute.For<INowPlayingService>();
        _nowPlaying.ReportAsync(_user.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NowPlayingStatus.TrackNotFound);
        _nowPlaying.ReportAsync(_user.Id, _trackId, Arg.Any<CancellationToken>())
            .Returns(NowPlayingStatus.Reported);
        _registry = Substitute.For<ISubsonicNowPlayingRegistry>();
        TimeProvider time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(_now);
        _service = new SubsonicAnnotationService(
            _favorites,
            _ratings,
            _history,
            _nowPlaying,
            _registry,
            Substitute.For<ITrackRepository>(),
            Substitute.For<ISubsonicRepository>(),
            time);
    }

    [Test]
    public async Task ScrobbleAsync_Submission_RecordsThePlayAtItsTime()
    {
        SubsonicResponse response = await _service.ScrobbleAsync(
            _user,
            TestParameters.Of(("id", SubsonicIds.Format(_trackId)), ("time", "1763459999000")));

        response.IsOk.Should().BeTrue();
        await _history.Received(1).RecordAsync(
            _user.Id,
            _trackId,
            DateTimeOffset.FromUnixTimeMilliseconds(1763459999000),
            Arg.Any<CancellationToken>());
        _registry.DidNotReceiveWithAnyArgs().Report(default, default, default);
    }

    [Test]
    public async Task ScrobbleAsync_WithoutTime_RecordsThePlayNow()
    {
        await _service.ScrobbleAsync(_user, TestParameters.Of(("id", SubsonicIds.Format(_trackId))));

        await _history.Received(1).RecordAsync(_user.Id, _trackId, null, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ScrobbleAsync_NoSubmission_ReportsNowPlaying()
    {
        SubsonicResponse response = await _service.ScrobbleAsync(
            _user,
            TestParameters.Of(("id", SubsonicIds.Format(_trackId)), ("submission", "false"), ("c", "Symfonium")));

        response.IsOk.Should().BeTrue();
        await _nowPlaying.Received(1).ReportAsync(_user.Id, _trackId, Arg.Any<CancellationToken>());
        _registry.Received(1).Report(_user.Id, _trackId, "Symfonium");
        await _history.DidNotReceiveWithAnyArgs().RecordAsync(default, default, default, default);
    }

    [Test]
    public async Task ScrobbleAsync_UnknownSong_FailsWithNotFound()
    {
        SubsonicResponse response = await _service.ScrobbleAsync(
            _user,
            TestParameters.Of(("id", SubsonicIds.Format(Guid.NewGuid()))));

        response.Error.Should().Be(SubsonicErrorCode.NotFound);
    }

    [Test]
    public async Task ScrobbleAsync_NoId_FailsWithMissingParameter()
    {
        SubsonicResponse response = await _service.ScrobbleAsync(_user, TestParameters.Of());

        response.Error.Should().Be(SubsonicErrorCode.MissingParameter);
    }

    [Test]
    public async Task StarAsync_PlainIdOfAnAlbum_StarsTheAlbum()
    {
        var albumId = Guid.NewGuid();
        _favorites.AddAsync(_user.Id, FavoriteType.Album, albumId, Arg.Any<CancellationToken>())
            .Returns(FavoriteStatus.Done);

        SubsonicResponse response = await _service.StarAsync(
            _user,
            TestParameters.Of(("id", SubsonicIds.Format(albumId))));

        response.IsOk.Should().BeTrue();
        await _favorites.Received(1).AddAsync(_user.Id, FavoriteType.Album, albumId, Arg.Any<CancellationToken>());
        await _favorites.DidNotReceive().AddAsync(
            _user.Id,
            FavoriteType.Artist,
            albumId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task StarAsync_ArtistId_StarsTheArtist()
    {
        var artistId = Guid.NewGuid();
        _favorites.AddAsync(_user.Id, FavoriteType.Artist, artistId, Arg.Any<CancellationToken>())
            .Returns(FavoriteStatus.Done);

        SubsonicResponse response = await _service.StarAsync(
            _user,
            TestParameters.Of(("artistId", SubsonicIds.Format(artistId))));

        response.IsOk.Should().BeTrue();
    }

    [Test]
    public async Task StarAsync_Nothing_FailsWithMissingParameter()
    {
        (await _service.StarAsync(_user, TestParameters.Of())).Error.Should().Be(SubsonicErrorCode.MissingParameter);
    }

    [Test]
    public async Task StarAsync_UnknownId_FailsWithNotFound()
    {
        (await _service.StarAsync(_user, TestParameters.Of(("id", SubsonicIds.Format(Guid.NewGuid())))))
            .Error.Should().Be(SubsonicErrorCode.NotFound);
    }

    [Test]
    public async Task SetRatingAsync_SetsTheRatingOfTheSong()
    {
        _ratings.SetAsync(_user.Id, _trackId, 4, Arg.Any<CancellationToken>()).Returns(RatingStatus.Done);

        SubsonicResponse response = await _service.SetRatingAsync(
            _user,
            TestParameters.Of(("id", SubsonicIds.Format(_trackId)), ("rating", "4")));

        response.IsOk.Should().BeTrue();
    }

    [TestCase("6")]
    [TestCase("-1")]
    [TestCase("five")]
    public async Task SetRatingAsync_InvalidRating_Fails(string rating)
    {
        SubsonicResponse response = await _service.SetRatingAsync(
            _user,
            TestParameters.Of(("id", SubsonicIds.Format(_trackId)), ("rating", rating)));

        response.Error.Should().Be(SubsonicErrorCode.Generic);
    }
}
