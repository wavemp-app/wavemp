using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Scrobbling.Clients;
using WaveMP.Core.Scrobbling.Connections;
using WaveMP.Core.Scrobbling.Options;
using WaveMP.Core.Scrobbling.Queues;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Scrobbling.Tests.Connections;

[TestFixture]
public class ScrobbleConnectionServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _userId = Guid.NewGuid();

    private IScrobbleRepository _repository = null!;
    private IScrobbleSender _sender = null!;
    private ScrobbleQueueSignal _signal = null!;
    private ScrobblingOptions _options = null!;
    private ScrobbleConnectionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = Substitute.For<IScrobbleRepository>();
        _repository.ListConnectionsAsync(_userId, Arg.Any<CancellationToken>()).Returns([]);
        _repository.CountQueuedAsync(_userId, Arg.Any<CancellationToken>()).Returns(new Dictionary<Guid, int>());
        _repository.SaveConnectionAsync(Arg.Any<ScrobbleConnection>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<ScrobbleConnection>());
        _sender = Substitute.For<IScrobbleSender>();
        _signal = new ScrobbleQueueSignal();
        _options = new ScrobblingOptions
        {
            LastFmApiKey = "api key",
            LastFmSharedSecret = "secret",
            LastFmAuthorizationUrl = new Uri("https://lastfm.test/api/auth/"),
        };
        TimeProvider time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(_now);
        _service = new ScrobbleConnectionService(
            _repository,
            _sender,
            new FakeProtector(),
            _signal,
            time,
            Microsoft.Extensions.Options.Options.Create(_options));
    }

    [TearDown]
    public void TearDown()
    {
        _signal.Dispose();
    }

    [Test]
    public async Task GetStatesAsync_NothingConnected_ListsEveryServiceWithTheLastFmPage()
    {
        IReadOnlyList<ScrobbleServiceState> states = await _service.GetStatesAsync(_userId);

        states.Should().BeEquivalentTo(
            [
                new ScrobbleServiceState(
                    ScrobbleService.LastFm,
                    true,
                    null,
                    0,
                    new Uri("https://lastfm.test/api/auth/?api_key=api%20key")),
                new ScrobbleServiceState(ScrobbleService.ListenBrainz, true, null, 0, null),
            ],
            options => options.WithStrictOrdering());
    }

    [Test]
    public async Task GetStatesAsync_LastFmNotConfigured_IsNotAvailable()
    {
        _options.LastFmApiKey = null;

        IReadOnlyList<ScrobbleServiceState> states = await _service.GetStatesAsync(_userId);

        states[0].IsAvailable.Should().BeFalse();
        states[0].AuthorizationUrl.Should().BeNull();
        states[1].IsAvailable.Should().BeTrue();
    }

    [Test]
    public async Task GetStatesAsync_Connected_AddsTheConnectionAndItsQueue()
    {
        ScrobbleConnection connection = Connection(ScrobbleService.ListenBrainz);
        _repository.ListConnectionsAsync(_userId, Arg.Any<CancellationToken>()).Returns([connection]);
        _repository.CountQueuedAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, int> { [connection.Id] = 3 });

        IReadOnlyList<ScrobbleServiceState> states = await _service.GetStatesAsync(_userId);

        states[1].Connection.Should().Be(connection);
        states[1].QueuedCount.Should().Be(3);
        states[0].Connection.Should().BeNull();
    }

    [Test]
    public async Task ConnectAsync_ValidToken_StoresTheProtectedCredentialAndWakesTheQueue()
    {
        _sender.AuthorizeAsync(ScrobbleService.LastFm, "token", Arg.Any<CancellationToken>())
            .Returns(ScrobbleAuthorization.Authorized("listener", "session-key"));

        ScrobbleConnectResult result = await _service.ConnectAsync(_userId, ScrobbleService.LastFm, " token ");

        result.Status.Should().Be(ScrobbleConnectStatus.Connected);
        result.State!.Connection.Should().BeEquivalentTo(new
        {
            UserId = _userId,
            Service = ScrobbleService.LastFm,
            AccountName = "listener",
            ProtectedCredential = FakeProtector.Prefix + "session-key",
            Status = ScrobbleConnectionStatus.Active,
            ConnectedAt = _now,
        });
        (await _signal.WaitAsync(TimeSpan.Zero, CancellationToken.None)).Should().BeTrue();
    }

    [TestCase(ScrobbleAuthorizationStatus.Invalid, ScrobbleConnectStatus.InvalidToken)]
    [TestCase(ScrobbleAuthorizationStatus.Unavailable, ScrobbleConnectStatus.ServiceUnavailable)]
    public async Task ConnectAsync_TokenNotAccepted_StoresNothing(
        ScrobbleAuthorizationStatus authorization,
        ScrobbleConnectStatus expected)
    {
        _sender.AuthorizeAsync(ScrobbleService.ListenBrainz, "token", Arg.Any<CancellationToken>())
            .Returns(new ScrobbleAuthorization(authorization));

        ScrobbleConnectResult result = await _service.ConnectAsync(_userId, ScrobbleService.ListenBrainz, "token");

        result.Status.Should().Be(expected);
        await _repository.DidNotReceiveWithAnyArgs().SaveConnectionAsync(default!, default);
    }

    [Test]
    public async Task ConnectAsync_LastFmNotConfigured_DoesNotAskLastFm()
    {
        _options.LastFmSharedSecret = " ";

        ScrobbleConnectResult result = await _service.ConnectAsync(_userId, ScrobbleService.LastFm, "token");

        result.Status.Should().Be(ScrobbleConnectStatus.NotConfigured);
        await _sender.DidNotReceiveWithAnyArgs().AuthorizeAsync(default, default!, default);
    }

    [Test]
    public async Task DisconnectAsync_Always_DeletesTheConnection()
    {
        _repository.DeleteConnectionAsync(_userId, ScrobbleService.LastFm, Arg.Any<CancellationToken>()).Returns(true);

        bool deleted = await _service.DisconnectAsync(_userId, ScrobbleService.LastFm);

        deleted.Should().BeTrue();
    }

    private ScrobbleConnection Connection(ScrobbleService service)
    {
        return new ScrobbleConnection
        {
            Id = Guid.NewGuid(),
            UserId = _userId,
            Service = service,
            AccountName = "listener",
            ProtectedCredential = FakeProtector.Prefix + "token",
            Status = ScrobbleConnectionStatus.Active,
            ConnectedAt = _now,
        };
    }
}
