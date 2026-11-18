using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Scrobbling.Clients;
using WaveMP.Core.Scrobbling.Plays;
using WaveMP.Core.Scrobbling.Tests.Clients;
using WaveMP.Core.Scrobbling.Tests.Queues;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scrobbling.Tests.Plays;

[TestFixture]
public class NowPlayingServiceTests
{
    private InMemoryScrobbleRepository _repository = null!;
    private ITrackRepository _tracks = null!;
    private IScrobbleSender _sender = null!;
    private NowPlayingService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new InMemoryScrobbleRepository();
        _tracks = Substitute.For<ITrackRepository>();
        _tracks.FindAsync(Arg.Any<Guid>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>()).Returns((TrackRow?)null);
        _sender = Substitute.For<IScrobbleSender>();
        _sender.SendNowPlayingAsync(default, default!, default!, default)
            .ReturnsForAnyArgs(ScrobbleSendStatus.Accepted);
        _service = new NowPlayingService(
            _tracks,
            _repository,
            _sender,
            new FakeProtector(),
            NullLogger<NowPlayingService>.Instance);
    }

    [Test]
    public async Task ReportAsync_ConnectedServices_ReportsTheSongToEveryActiveOne()
    {
        _repository.AddConnection(ScrobbleService.LastFm, FakeProtector.Prefix + "key");
        _repository.AddConnection(ScrobbleService.ListenBrainz, FakeProtector.Prefix + "token").Status =
            ScrobbleConnectionStatus.AuthorizationFailed;
        TrackRow track = Track(TimeSpan.FromMinutes(3));

        NowPlayingStatus status = await _service.ReportAsync(_repository.UserId, track.Id);

        status.Should().Be(NowPlayingStatus.Reported);
        await _sender.Received(1).SendNowPlayingAsync(
            ScrobbleService.LastFm,
            "key",
            new ScrobbleTrack("Artist", "Title", "Album", null, null, TimeSpan.FromMinutes(3)),
            Arg.Any<CancellationToken>());
        await _sender.DidNotReceive().SendNowPlayingAsync(
            ScrobbleService.ListenBrainz,
            Arg.Any<string>(),
            Arg.Any<ScrobbleTrack>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ReportAsync_SongShorterThanThirtySeconds_ReportsNothing()
    {
        _repository.AddConnection(ScrobbleService.LastFm, FakeProtector.Prefix + "key");
        TrackRow track = Track(TimeSpan.FromSeconds(20));

        NowPlayingStatus status = await _service.ReportAsync(_repository.UserId, track.Id);

        status.Should().Be(NowPlayingStatus.NotReported);
        await _sender.DidNotReceiveWithAnyArgs().SendNowPlayingAsync(default, default!, default!, default);
    }

    [Test]
    public async Task ReportAsync_UnknownTrack_ReturnsTrackNotFound()
    {
        NowPlayingStatus status = await _service.ReportAsync(_repository.UserId, Guid.NewGuid());

        status.Should().Be(NowPlayingStatus.TrackNotFound);
    }

    [Test]
    public async Task ReportAsync_CredentialsRejected_MarksTheConnection()
    {
        _repository.AddConnection(ScrobbleService.LastFm, FakeProtector.Prefix + "key");
        _sender.SendNowPlayingAsync(default, default!, default!, default)
            .ReturnsForAnyArgs(ScrobbleSendStatus.Unauthorized);
        TrackRow track = Track(TimeSpan.FromMinutes(3));

        NowPlayingStatus status = await _service.ReportAsync(_repository.UserId, track.Id);

        status.Should().Be(NowPlayingStatus.NotReported);
        _repository.Connections.Single().Status.Should().Be(ScrobbleConnectionStatus.AuthorizationFailed);
    }

    private TrackRow Track(TimeSpan duration)
    {
        TrackRow track = ScrobbleTracksTests.Track("Title", "Artist", "Album", null) with { Duration = duration };
        _tracks.FindAsync(track.Id, _repository.UserId, Arg.Any<CancellationToken>()).Returns(track);
        return track;
    }
}
