using System.Net;
using System.Text.Json.Nodes;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WaveMP.Core.Scrobbling.Clients;

namespace WaveMP.Core.Scrobbling.Tests.Clients;

/// <summary>
/// Runs the client against a simulated ListenBrainz; no request leaves the process.
/// </summary>
[TestFixture]
public class ListenBrainzClientTests
{
    private static readonly ScrobbleTrack _track = new(
        "Artist",
        "Title",
        "Album",
        "Album Artist",
        7,
        TimeSpan.FromSeconds(180.5));

    private StubHandler _handler = null!;
    private HttpClient _http = null!;
    private ListenBrainzClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new StubHandler();
        _http = new HttpClient(_handler) { BaseAddress = new Uri("https://listenbrainz.test/") };
        _client = new ListenBrainzClient(_http, NullLogger<ListenBrainzClient>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _http.Dispose();
        _handler.Dispose();
    }

    [Test]
    public async Task ValidateTokenAsync_ValidToken_ReturnsTheUserAndKeepsTheToken()
    {
        _handler.Respond(
            HttpStatusCode.OK,
            """{"code":200,"message":"Token valid.","valid":true,"user_name":"listener"}""");

        ScrobbleAuthorization authorization = await _client.ValidateTokenAsync("user-token");

        authorization.Should().Be(ScrobbleAuthorization.Authorized("listener", "user-token"));
        StubHandler.RecordedRequest request = _handler.Requests.Should().ContainSingle().Which;
        request.Method.Should().Be(HttpMethod.Get);
        request.Url.Should().Be("https://listenbrainz.test/1/validate-token");
        request.Authorization.Should().Be("Token user-token");
    }

    [Test]
    public async Task ValidateTokenAsync_InvalidToken_IsInvalid()
    {
        _handler.Respond(HttpStatusCode.OK, """{"code":200,"message":"Token invalid.","valid":false}""");

        ScrobbleAuthorization authorization = await _client.ValidateTokenAsync("wrong");

        authorization.Status.Should().Be(ScrobbleAuthorizationStatus.Invalid);
    }

    [Test]
    public async Task ValidateTokenAsync_Unauthorized_IsInvalid()
    {
        _handler.Respond(HttpStatusCode.Unauthorized, """{"code":401,"error":"Invalid authorization token."}""");

        ScrobbleAuthorization authorization = await _client.ValidateTokenAsync("wrong");

        authorization.Status.Should().Be(ScrobbleAuthorizationStatus.Invalid);
    }

    [Test]
    public async Task ValidateTokenAsync_NetworkError_IsUnavailable()
    {
        _handler.Fail(new HttpRequestException("No route to host"));

        ScrobbleAuthorization authorization = await _client.ValidateTokenAsync("user-token");

        authorization.Status.Should().Be(ScrobbleAuthorizationStatus.Unavailable);
    }

    [Test]
    public async Task SubmitPlayingNowAsync_Track_SendsPlayingNowWithoutTime()
    {
        _handler.Respond(HttpStatusCode.OK, """{"status":"ok"}""");

        ScrobbleSendStatus status = await _client.SubmitPlayingNowAsync("user-token", _track);

        status.Should().Be(ScrobbleSendStatus.Accepted);
        StubHandler.RecordedRequest request = _handler.Requests.Single();
        request.Url.Should().Be("https://listenbrainz.test/1/submit-listens");
        request.Authorization.Should().Be("Token user-token");
        JsonNode body = JsonNode.Parse(request.Body!)!;
        body["listen_type"]!.GetValue<string>().Should().Be("playing_now");
        JsonNode listen = body["payload"]![0]!;
        listen["listened_at"].Should().BeNull();
        listen["track_metadata"]!["artist_name"]!.GetValue<string>().Should().Be("Artist");
        listen["track_metadata"]!["track_name"]!.GetValue<string>().Should().Be("Title");
        listen["track_metadata"]!["release_name"]!.GetValue<string>().Should().Be("Album");
        JsonNode info = listen["track_metadata"]!["additional_info"]!;
        info["duration_ms"]!.GetValue<long>().Should().Be(180500);
        info["tracknumber"]!.GetValue<int>().Should().Be(7);
        info["submission_client"]!.GetValue<string>().Should().Be("WaveMP");
    }

    [Test]
    public async Task SubmitListensAsync_OnePlay_SendsASingleListenWithItsStart()
    {
        _handler.Respond(HttpStatusCode.OK, """{"status":"ok"}""");
        DateTimeOffset playedAt = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

        ScrobbleSendStatus status = await _client.SubmitListensAsync(
            "user-token",
            [new ScrobblePlay(_track, playedAt)]);

        status.Should().Be(ScrobbleSendStatus.Accepted);
        JsonNode body = JsonNode.Parse(_handler.Requests.Single().Body!)!;
        body["listen_type"]!.GetValue<string>().Should().Be("single");
        body["payload"]![0]!["listened_at"]!.GetValue<long>().Should().Be(playedAt.ToUnixTimeSeconds());
    }

    [Test]
    public async Task SubmitListensAsync_SeveralPlays_SendsAnImport()
    {
        _handler.Respond(HttpStatusCode.OK, """{"status":"ok"}""");
        DateTimeOffset playedAt = new(2026, 11, 18, 10, 0, 0, TimeSpan.Zero);

        await _client.SubmitListensAsync(
            "user-token",
            [
                new ScrobblePlay(_track, playedAt),
                new ScrobblePlay(new ScrobbleTrack("A", "B"), playedAt.AddMinutes(3)),
            ]);

        JsonNode body = JsonNode.Parse(_handler.Requests.Single().Body!)!;
        body["listen_type"]!.GetValue<string>().Should().Be("import");
        body["payload"]!.AsArray().Should().HaveCount(2);
        body["payload"]![1]!["track_metadata"]!["release_name"].Should().BeNull();
    }

    [TestCase(HttpStatusCode.Unauthorized, ScrobbleSendStatus.Unauthorized)]
    [TestCase(HttpStatusCode.BadRequest, ScrobbleSendStatus.Rejected)]
    [TestCase(HttpStatusCode.TooManyRequests, ScrobbleSendStatus.Unavailable)]
    [TestCase(HttpStatusCode.BadGateway, ScrobbleSendStatus.Unavailable)]
    public async Task SubmitListensAsync_Error_MapsTheStatusCode(HttpStatusCode code, ScrobbleSendStatus expected)
    {
        _handler.Respond(code, """{"code":0,"error":"Error"}""");

        ScrobbleSendStatus status = await _client.SubmitListensAsync(
            "user-token",
            [new ScrobblePlay(_track, DateTimeOffset.UtcNow)]);

        status.Should().Be(expected);
    }

    [Test]
    public async Task SubmitListensAsync_NetworkError_IsUnavailable()
    {
        _handler.Fail(new HttpRequestException("No route to host"));

        ScrobbleSendStatus status = await _client.SubmitListensAsync(
            "user-token",
            [new ScrobblePlay(_track, DateTimeOffset.UtcNow)]);

        status.Should().Be(ScrobbleSendStatus.Unavailable);
    }
}
