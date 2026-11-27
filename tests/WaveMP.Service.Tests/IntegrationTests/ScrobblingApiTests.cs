using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WaveMP.Core.Scrobbling.Clients;
using WaveMP.Data;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Connects profiles to simulated scrobbling services (NSubstitute) and scrobbles plays of a small library of
/// synthetic media through the API. No request leaves the process.
/// </summary>
[TestFixture]
[Category("Integration")]
public class ScrobblingApiTests
{
    private const string Path = "/api/v1/me/scrobbling";
    private const string DatabaseName = "wavemp_tests_service_scrobbling";
    private const string NoKeyDatabaseName = "wavemp_tests_service_scrobbling_nokey";
    private const string RestartDatabaseName = "wavemp_tests_service_scrobbling_restart";

    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(20);

    private TemporaryDirectory _media = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private IReadOnlyList<TrackDto> _tracks = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = await CreateMediaAsync();
        _factory = new WaveMpWebApplicationFactory(
            DatabaseName,
            freshDatabase: true,
            mediaPath: _media.Path,
            settings: Settings(lastFm: true));
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        _tracks = await ScanAsync(_admin);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
        _media.Dispose();
    }

    [Test]
    public async Task Scrobbling_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage read = await _anonymous.GetAsync(Path);
        HttpResponseMessage connect = await _anonymous.PutAsJsonAsync($"{Path}/ListenBrainz", new { token = "x" });
        HttpResponseMessage disconnect = await _anonymous.DeleteAsync($"{Path}/ListenBrainz");
        HttpResponseMessage nowPlaying = await _anonymous.PutAsJsonAsync(
            "/api/v1/me/now-playing",
            new { trackId = Track("Long Song") });

        await read.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await connect.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await disconnect.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await nowPlaying.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task GetMyScrobbling_NothingConnected_ListsBothServicesWithTheLastFmPage()
    {
        using HttpClient client = await CreateProfileAsync();

        ScrobblingDto scrobbling = await client.GetJsonAsync<ScrobblingDto>(Path);

        scrobbling.Services.Select(service => service.Service).Should().Equal(
            ScrobbleServiceDto.LastFm,
            ScrobbleServiceDto.ListenBrainz);
        scrobbling.Services.Should().AllSatisfy(service =>
        {
            service.IsAvailable.Should().BeTrue();
            service.Status.Should().Be(ScrobbleConnectionStatusDto.NotConnected);
            service.QueuedScrobbles.Should().Be(0);
        });
        scrobbling.Services[0].AuthorizationUrl.Should().Be(new Uri("https://www.last.fm/api/auth/?api_key=test-key"));
        scrobbling.Services[1].AuthorizationUrl.Should().BeNull();
    }

    [Test]
    public async Task ConnectMyScrobblingService_ValidToken_ConnectsAndStoresTheCredentialEncrypted()
    {
        using HttpClient client = await CreateProfileAsync();
        string token = "lb-" + HttpClientExtensions.Unique();
        _factory.ListenBrainz.ValidateTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(ScrobbleAuthorization.Authorized("listener", token));

        HttpResponseMessage response = await client.PutAsJsonAsync($"{Path}/ListenBrainz", new { token });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        ScrobblingServiceDto service =
            (await response.Content.ReadFromJsonAsync<ScrobblingServiceDto>(HttpClientExtensions.JsonOptions))!;
        service.Status.Should().Be(ScrobbleConnectionStatusDto.Connected);
        service.AccountName.Should().Be("listener");
        service.ConnectedAt.Should().NotBeNull();
        (await response.Content.ReadAsStringAsync()).Should().NotContain(token);

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        WaveMpDbContext db = scope.ServiceProvider.GetRequiredService<WaveMpDbContext>();
        ScrobbleConnection stored = await db.ScrobbleConnections.SingleAsync(connection =>
            connection.AccountName == "listener" && connection.ConnectedAt == service.ConnectedAt);
        stored.ProtectedCredential.Should().NotContain(token);
    }

    [Test]
    public async Task ConnectMyScrobblingService_LastFmToken_ExchangesItForASession()
    {
        using HttpClient client = await CreateProfileAsync();
        string token = "fm-" + HttpClientExtensions.Unique();
        _factory.LastFm.GetSessionAsync(token, Arg.Any<CancellationToken>())
            .Returns(ScrobbleAuthorization.Authorized("fm-listener", "session-key"));

        HttpResponseMessage response = await client.PutAsJsonAsync($"{Path}/LastFm", new { token });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        ScrobblingDto scrobbling = await client.GetJsonAsync<ScrobblingDto>(Path);
        scrobbling.Services[0].Should().BeEquivalentTo(new
        {
            Service = ScrobbleServiceDto.LastFm,
            Status = ScrobbleConnectionStatusDto.Connected,
            AccountName = "fm-listener",
        });
    }

    [Test]
    public async Task ConnectMyScrobblingService_TokenRejected_ReturnsTokenInvalid()
    {
        using HttpClient client = await CreateProfileAsync();
        _factory.ListenBrainz.ValidateTokenAsync("wrong", Arg.Any<CancellationToken>())
            .Returns(ScrobbleAuthorization.Invalid);

        HttpResponseMessage response = await client.PutAsJsonAsync($"{Path}/ListenBrainz", new { token = "wrong" });

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "scrobbling.token_invalid");
    }

    [Test]
    public async Task ConnectMyScrobblingService_ServiceDown_ReturnsServiceUnavailable()
    {
        using HttpClient client = await CreateProfileAsync();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            $"{Path}/ListenBrainz",
            new { token = "unreachable" });

        await response.ShouldBeProblemAsync(HttpStatusCode.ServiceUnavailable, "scrobbling.service_unavailable");
    }

    [Test]
    public async Task ConnectMyScrobblingService_UnknownService_ReturnsInvalidValue()
    {
        using HttpClient client = await CreateProfileAsync();

        HttpResponseMessage response = await client.PutAsJsonAsync($"{Path}/Spotify", new { token = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ConnectMyScrobblingService_WithoutToken_ReturnsRequired()
    {
        using HttpClient client = await CreateProfileAsync();

        HttpResponseMessage response = await client.PutAsJsonAsync($"{Path}/ListenBrainz", new { token = " " });

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Field = "token", Code = ValidationErrorCodeDto.Required });
    }

    [Test]
    public async Task DisconnectMyScrobblingService_Connected_RemovesTheConnection()
    {
        using HttpClient client = await CreateProfileAsync();
        await ConnectListenBrainzAsync(client);

        HttpResponseMessage first = await client.DeleteAsync($"{Path}/ListenBrainz");
        HttpResponseMessage second = await client.DeleteAsync($"{Path}/ListenBrainz");

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await second.ShouldBeProblemAsync(HttpStatusCode.NotFound, "scrobbling.not_connected");
        ScrobblingDto scrobbling = await client.GetJsonAsync<ScrobblingDto>(Path);
        scrobbling.Services[1].Status.Should().Be(ScrobbleConnectionStatusDto.NotConnected);
    }

    [Test]
    public async Task CreateMyHistoryEntry_ConnectedProfile_ScrobblesThePlay()
    {
        using HttpClient client = await CreateProfileAsync();
        string token = await ConnectListenBrainzAsync(client);
        _factory.ListenBrainz
            .SubmitListensAsync(token, Arg.Any<IReadOnlyList<ScrobblePlay>>(), Arg.Any<CancellationToken>())
            .Returns(ScrobbleSendStatus.Accepted);
        DateTimeOffset playedAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        await RecordPlayAsync(client, Track("Long Song"), playedAt);

        await WaitUntilAsync(async () =>
            (await client.GetJsonAsync<ScrobblingDto>(Path)).Services[1].LastScrobbledAt is not null);
        await _factory.ListenBrainz.Received(1).SubmitListensAsync(
            token,
            Arg.Is<IReadOnlyList<ScrobblePlay>>(plays => plays.Count == 1
                && plays[0].Track.Title == "Long Song"
                && plays[0].Track.Artist == "Scrobble Artist"
                && plays[0].Track.Album == "Scrobble Album"
                && Math.Abs((plays[0].PlayedAt - playedAt).TotalSeconds) < 1),
            Arg.Any<CancellationToken>());
        (await client.GetJsonAsync<ScrobblingDto>(Path)).Services[1].QueuedScrobbles.Should().Be(0);
    }

    [Test]
    public async Task CreateMyHistoryEntry_SongShorterThanThirtySeconds_IsNotScrobbled()
    {
        using HttpClient client = await CreateProfileAsync();
        string token = await ConnectListenBrainzAsync(client);

        await RecordPlayAsync(client, Track("Jingle"), DateTimeOffset.UtcNow);

        ScrobblingDto scrobbling = await client.GetJsonAsync<ScrobblingDto>(Path);
        scrobbling.Services[1].QueuedScrobbles.Should().Be(0);
        await _factory.ListenBrainz.DidNotReceive().SubmitListensAsync(
            token,
            Arg.Any<IReadOnlyList<ScrobblePlay>>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task CreateMyHistoryEntry_ServiceDown_KeepsTheScrobbleAndSendsItLater()
    {
        using HttpClient client = await CreateProfileAsync();
        string token = await ConnectListenBrainzAsync(client);
        int calls = 0;
        _factory.ListenBrainz
            .SubmitListensAsync(token, Arg.Any<IReadOnlyList<ScrobblePlay>>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref calls) == 1
                ? ScrobbleSendStatus.Unavailable
                : ScrobbleSendStatus.Accepted);

        await RecordPlayAsync(client, Track("Long Song"), DateTimeOffset.UtcNow);

        await WaitUntilAsync(async () =>
            (await client.GetJsonAsync<ScrobblingDto>(Path)).Services[1].LastScrobbledAt is not null);
        calls.Should().BeGreaterThanOrEqualTo(2);
        (await client.GetJsonAsync<ScrobblingDto>(Path)).Services[1].QueuedScrobbles.Should().Be(0);
    }

    [Test]
    public async Task UpdateMyNowPlaying_ConnectedProfile_ReportsTheSong()
    {
        using HttpClient client = await CreateProfileAsync();
        string token = await ConnectListenBrainzAsync(client);
        _factory.ListenBrainz.SubmitPlayingNowAsync(token, Arg.Any<ScrobbleTrack>(), Arg.Any<CancellationToken>())
            .Returns(ScrobbleSendStatus.Accepted);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/me/now-playing",
            new { trackId = Track("Long Song") });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await _factory.ListenBrainz.Received(1).SubmitPlayingNowAsync(
            token,
            Arg.Is<ScrobbleTrack>(track => track.Title == "Long Song"),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UpdateMyNowPlaying_UnknownTrack_ReturnsNotFound()
    {
        using HttpClient client = await CreateProfileAsync();

        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/me/now-playing",
            new { trackId = Guid.NewGuid() });

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "track.not_found");
    }

    [Test]
    public async Task ConnectMyScrobblingService_LastFmWithoutApiAccount_ReturnsNotConfigured()
    {
        await using WaveMpWebApplicationFactory factory = new(
            NoKeyDatabaseName,
            freshDatabase: true,
            settings: Settings(lastFm: false));
        using HttpClient client = await factory.CreateAdminClientAsync();

        ScrobblingDto scrobbling = await client.GetJsonAsync<ScrobblingDto>(Path);
        HttpResponseMessage response = await client.PutAsJsonAsync($"{Path}/LastFm", new { token = "x" });

        scrobbling.Services[0].IsAvailable.Should().BeFalse();
        scrobbling.Services[0].AuthorizationUrl.Should().BeNull();
        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "scrobbling.not_configured");
        await factory.DeleteDatabaseAsync();
    }

    [Test]
    public async Task Restart_WithQueuedScrobble_SendsItAfterTheStart()
    {
        using TemporaryDirectory media = await CreateMediaAsync();
        using TemporaryDirectory data = new();
        Dictionary<string, string?> settings = Settings(lastFm: false);
        settings["DATA_PATH"] = data.Path;
        string sessionToken;
        await using (WaveMpWebApplicationFactory first = new(
            RestartDatabaseName,
            freshDatabase: true,
            mediaPath: media.Path,
            settings: settings))
        {
            using HttpClient admin = await first.CreateAdminClientAsync();
            IReadOnlyList<TrackDto> tracks = await ScanAsync(admin);
            first.ListenBrainz.ValidateTokenAsync("restart-token", Arg.Any<CancellationToken>())
                .Returns(ScrobbleAuthorization.Authorized("listener", "restart-token"));
            (await admin.PutAsJsonAsync($"{Path}/ListenBrainz", new { token = "restart-token" }))
                .StatusCode.Should().Be(HttpStatusCode.OK);

            await RecordPlayAsync(
                admin,
                tracks.Single(track => track.Title == "Long Song").Id,
                DateTimeOffset.UtcNow);

            (await admin.GetJsonAsync<ScrobblingDto>(Path)).Services[1].QueuedScrobbles.Should().Be(1);
            sessionToken = admin.DefaultRequestHeaders.Authorization!.Parameter!;
        }

        IListenBrainzClient listenBrainz = WaveMpWebApplicationFactory.CreateListenBrainz();
        listenBrainz
            .SubmitListensAsync("restart-token", Arg.Any<IReadOnlyList<ScrobblePlay>>(), Arg.Any<CancellationToken>())
            .Returns(ScrobbleSendStatus.Accepted);
        await using WaveMpWebApplicationFactory second = new(
            RestartDatabaseName,
            mediaPath: media.Path,
            settings: settings,
            listenBrainz: listenBrainz);
        using HttpClient client = second.CreateAuthorizedClient(sessionToken);

        await WaitUntilAsync(async () =>
            (await client.GetJsonAsync<ScrobblingDto>(Path)).Services[1].QueuedScrobbles == 0);
        await listenBrainz.Received(1).SubmitListensAsync(
            "restart-token",
            Arg.Is<IReadOnlyList<ScrobblePlay>>(plays => plays.Count == 1 && plays[0].Track.Title == "Long Song"),
            Arg.Any<CancellationToken>());
        await second.DeleteDatabaseAsync();
    }

    private static Dictionary<string, string?> Settings(bool lastFm)
    {
        return new Dictionary<string, string?>
        {
            ["LASTFM_API_KEY"] = lastFm ? "test-key" : string.Empty,
            ["LASTFM_SHARED_SECRET"] = lastFm ? "test-secret" : string.Empty,
            ["Scrobbling:StartupDelay"] = "00:00:00",
            ["Scrobbling:RetryDelay"] = "00:00:00.200",
            ["Scrobbling:MaxRetryDelay"] = "00:00:00.500",
        };
    }

    private static async Task<TemporaryDirectory> CreateMediaAsync()
    {
        TemporaryDirectory media = new();
        await TestMediaGenerator.CreateAudioAsync(
            media.Combine("Long Song.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = "Long Song", Artist = "Scrobble Artist", Album = "Scrobble Album" },
            durationSeconds: 31);
        await TestMediaGenerator.CreateAudioAsync(
            media.Combine("Jingle.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = "Jingle", Artist = "Scrobble Artist" },
            durationSeconds: 2);
        return media;
    }

    private static async Task<IReadOnlyList<TrackDto>> ScanAsync(HttpClient admin)
    {
        HttpResponseMessage response = await admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        ScanDto scan = await admin.WaitForScanAsync(queued.Id);
        scan.Status.Should().Be(JobRunStatusDto.Succeeded);
        return (await admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100")).Items;
    }

    private static async Task RecordPlayAsync(HttpClient client, Guid trackId, DateTimeOffset playedAt)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/me/history",
            new { trackId, playedAt },
            HttpClientExtensions.JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        DateTimeOffset end = DateTimeOffset.UtcNow + _timeout;
        while (!await condition())
        {
            if (DateTimeOffset.UtcNow > end)
            {
                Assert.Fail("The condition was not met in time.");
            }

            await Task.Delay(100);
        }
    }

    private async Task<string> ConnectListenBrainzAsync(HttpClient client)
    {
        string token = "lb-" + HttpClientExtensions.Unique();
        _factory.ListenBrainz.ValidateTokenAsync(token, Arg.Any<CancellationToken>())
            .Returns(ScrobbleAuthorization.Authorized("listener", token));
        HttpResponseMessage response = await client.PutAsJsonAsync($"{Path}/ListenBrainz", new { token });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return token;
    }

    private async Task<HttpClient> CreateProfileAsync()
    {
        UserDto user = await _admin.CreateUserAsync(new { name = $"Scrobbler {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        return _factory.CreateAuthorizedClient(session.Token);
    }

    private Guid Track(string title)
    {
        return _tracks.Single(track => track.Title == title).Id;
    }
}
