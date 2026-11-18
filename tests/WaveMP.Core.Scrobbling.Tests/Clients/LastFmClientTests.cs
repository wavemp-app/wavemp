using System.Net;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WaveMP.Core.Scrobbling.Clients;
using WaveMP.Core.Scrobbling.Options;

namespace WaveMP.Core.Scrobbling.Tests.Clients;

/// <summary>
/// Runs the client against a simulated Last.fm; no request leaves the process.
/// </summary>
[TestFixture]
public class LastFmClientTests
{
    private static readonly ScrobbleTrack _track = new(
        "Band & Co",
        "Sön g",
        "Album",
        "Album Artist",
        3,
        TimeSpan.FromSeconds(199.6));

    private StubHandler _handler = null!;
    private HttpClient _http = null!;
    private ScrobblingOptions _options = null!;
    private LastFmClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new StubHandler();
        _http = new HttpClient(_handler) { BaseAddress = new Uri("https://lastfm.test/2.0/") };
        _options = new ScrobblingOptions { LastFmApiKey = "key", LastFmSharedSecret = "secret" };
        _client = new LastFmClient(
            _http,
            Microsoft.Extensions.Options.Options.Create(_options),
            NullLogger<LastFmClient>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _http.Dispose();
        _handler.Dispose();
    }

    [Test]
    public void Sign_Parameters_HashesThemSortedByNameWithTheSecret()
    {
        string signature = LastFmClient.Sign(
            [
                new("token", "abc"),
                new("method", "auth.getSession"),
                new("format", "json"),
                new("api_key", "key"),
            ],
            "secret");

        signature.Should().Be("6629efc98b97f7c35ff32314185ffaa1");
    }

    [Test]
    public async Task GetSessionAsync_ValidToken_PostsTheSignedRequestAndReadsTheSession()
    {
        _handler.Respond(HttpStatusCode.OK, """{"session":{"subscriber":0,"name":"listener","key":"session-key"}}""");

        ScrobbleAuthorization authorization = await _client.GetSessionAsync("abc");

        authorization.Should().Be(ScrobbleAuthorization.Authorized("listener", "session-key"));
        StubHandler.RecordedRequest request = _handler.Requests.Should().ContainSingle().Which;
        request.Method.Should().Be(HttpMethod.Post);
        request.Url.Should().Be("https://lastfm.test/2.0/");
        Form(request).Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["method"] = "auth.getSession",
            ["api_key"] = "key",
            ["token"] = "abc",
            ["api_sig"] = "6629efc98b97f7c35ff32314185ffaa1",
            ["format"] = "json",
        });
    }

    [TestCase(4)]
    [TestCase(14)]
    [TestCase(15)]
    public async Task GetSessionAsync_TokenRejected_IsInvalid(int error)
    {
        _handler.Respond(HttpStatusCode.Forbidden, $$"""{"error":{{error}},"message":"Unauthorized Token"}""");

        ScrobbleAuthorization authorization = await _client.GetSessionAsync("abc");

        authorization.Status.Should().Be(ScrobbleAuthorizationStatus.Invalid);
    }

    [Test]
    public async Task GetSessionAsync_ServiceDown_IsUnavailable()
    {
        _handler.Respond(HttpStatusCode.ServiceUnavailable, "<html>down</html>");

        ScrobbleAuthorization authorization = await _client.GetSessionAsync("abc");

        authorization.Status.Should().Be(ScrobbleAuthorizationStatus.Unavailable);
    }

    [Test]
    public async Task GetSessionAsync_NotConfigured_SendsNothing()
    {
        _options.LastFmSharedSecret = null;

        ScrobbleAuthorization authorization = await _client.GetSessionAsync("abc");

        authorization.Status.Should().Be(ScrobbleAuthorizationStatus.Unavailable);
        _handler.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task UpdateNowPlayingAsync_Track_SendsTheTrackWithTheSessionKey()
    {
        _handler.Respond(HttpStatusCode.OK, """{"nowplaying":{}}""");

        ScrobbleSendStatus status = await _client.UpdateNowPlayingAsync("session-key", _track);

        status.Should().Be(ScrobbleSendStatus.Accepted);
        Dictionary<string, string> form = Form(_handler.Requests.Single());
        form.Should().Contain(new Dictionary<string, string>
        {
            ["method"] = "track.updateNowPlaying",
            ["sk"] = "session-key",
            ["artist"] = "Band & Co",
            ["track"] = "Sön g",
            ["album"] = "Album",
            ["albumArtist"] = "Album Artist",
            ["trackNumber"] = "3",
            ["duration"] = "200",
        });
        form["api_sig"].Should().Be(LastFmClient.Sign(form.Where(pair => pair.Key != "api_sig"), "secret"));
    }

    [Test]
    public async Task ScrobbleAsync_SeveralPlays_SendsThemIndexedWithTheirStart()
    {
        _handler.Respond(
            HttpStatusCode.OK,
            """{"scrobbles":{"@attr":{"accepted":2,"ignored":0},"scrobble":[]}}""");
        DateTimeOffset first = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);
        ScrobbleTrack minimal = new("Artist", "Title");

        ScrobbleSendStatus status = await _client.ScrobbleAsync(
            "session-key",
            [new ScrobblePlay(_track, first), new ScrobblePlay(minimal, first.AddMinutes(4))]);

        status.Should().Be(ScrobbleSendStatus.Accepted);
        Dictionary<string, string> form = Form(_handler.Requests.Single());
        form.Should().Contain(new Dictionary<string, string>
        {
            ["method"] = "track.scrobble",
            ["artist[0]"] = "Band & Co",
            ["track[0]"] = "Sön g",
            ["timestamp[0]"] = first.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["artist[1]"] = "Artist",
            ["track[1]"] = "Title",
            ["timestamp[1]"] = first.AddMinutes(4).ToUnixTimeSeconds()
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
        });
        form.Should().NotContainKey("album[1]");
    }

    [Test]
    public async Task ScrobbleAsync_NoPlays_SendsNothing()
    {
        ScrobbleSendStatus status = await _client.ScrobbleAsync("session-key", []);

        status.Should().Be(ScrobbleSendStatus.Accepted);
        _handler.Requests.Should().BeEmpty();
    }

    [TestCase(9, ScrobbleSendStatus.Unauthorized)]
    [TestCase(6, ScrobbleSendStatus.Rejected)]
    [TestCase(11, ScrobbleSendStatus.Unavailable)]
    [TestCase(16, ScrobbleSendStatus.Unavailable)]
    [TestCase(29, ScrobbleSendStatus.Unavailable)]
    [TestCase(10, ScrobbleSendStatus.Unavailable)]
    [TestCase(26, ScrobbleSendStatus.Unavailable)]
    public async Task ScrobbleAsync_Error_MapsTheErrorCode(int error, ScrobbleSendStatus expected)
    {
        _handler.Respond(HttpStatusCode.BadRequest, $$"""{"error":{{error}},"message":"Error"}""");

        ScrobbleSendStatus status = await _client.ScrobbleAsync(
            "session-key",
            [new ScrobblePlay(_track, DateTimeOffset.UtcNow)]);

        status.Should().Be(expected);
    }

    [Test]
    public async Task ScrobbleAsync_NetworkError_IsUnavailable()
    {
        _handler.Fail(new HttpRequestException("No route to host"));

        ScrobbleSendStatus status = await _client.ScrobbleAsync(
            "session-key",
            [new ScrobblePlay(_track, DateTimeOffset.UtcNow)]);

        status.Should().Be(ScrobbleSendStatus.Unavailable);
    }

    private static Dictionary<string, string> Form(StubHandler.RecordedRequest request)
    {
        return request.Body!.Split('&').Select(pair => pair.Split('=')).ToDictionary(
            pair => Uri.UnescapeDataString(pair[0].Replace('+', ' ')),
            pair => Uri.UnescapeDataString(pair[1].Replace('+', ' ')));
    }
}
