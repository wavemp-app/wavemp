using System.Net;
using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Core.Lyrics.Options;

namespace WaveMP.Core.Lyrics.Tests.Downloads;

/// <summary>
/// Runs the client against a simulated LRCLIB; no request leaves the process.
/// </summary>
[TestFixture]
public class LrclibClientTests
{
    private static readonly LrclibQuery _query = new("Sön g", "Band & Co", "Album", TimeSpan.FromSeconds(199.6));
    private const string EasyTarget = "0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF";
    private static readonly LrclibPublication _publication = new(
        "Song",
        "Band",
        "Album",
        TimeSpan.FromSeconds(199.6),
        "Line",
        "[00:01.00]Line");

    private StubHandler _handler = null!;
    private HttpClient _http = null!;
    private LrclibClient _client = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new StubHandler();
        _http = new HttpClient(_handler) { BaseAddress = new Uri("https://lyrics.test/") };
        _client = new LrclibClient(
            _http,
            new LrclibRequestPacer(
                TimeProvider.System,
                Microsoft.Extensions.Options.Options.Create(new LyricsOptions { RequestInterval = TimeSpan.Zero })),
            TimeSpan.FromSeconds(30),
            NullLogger<LrclibClient>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _http.Dispose();
        _handler.Dispose();
    }

    [Test]
    public async Task GetAsync_FoundRecord_SendsTheEncodedQueryAndReadsTheRecord()
    {
        _handler.Respond(HttpStatusCode.OK, """
            {"id":7,"trackName":"Sön g","artistName":"Band & Co","albumName":"Album","duration":200.0,
             "instrumental":false,"plainLyrics":"Line","syncedLyrics":"[00:01.00]Line"}
            """);

        LrclibResponse response = await _client.GetAsync(_query);

        _handler.Requests.Should().ContainSingle().Which.Should().Be(
            "https://lyrics.test/api/get?track_name=S%C3%B6n%20g&artist_name=Band%20%26%20Co"
                + "&album_name=Album&duration=200");
        response.Status.Should().Be(LrclibStatus.Found);
        response.Records.Should().ContainSingle().Which.Should().BeEquivalentTo(new LrclibRecord
        {
            Id = 7,
            TrackName = "Sön g",
            ArtistName = "Band & Co",
            AlbumName = "Album",
            Duration = 200,
            PlainLyrics = "Line",
            SyncedLyrics = "[00:01.00]Line",
        });
    }

    [Test]
    public async Task SearchAsync_Records_SendsTitleAndArtistAndReadsAllRecords()
    {
        _handler.Respond(HttpStatusCode.OK, """[{"id":1,"plainLyrics":"A"},{"id":2,"instrumental":true}]""");

        LrclibResponse response = await _client.SearchAsync(_query with { ArtistName = null });

        _handler.Requests.Should().ContainSingle()
            .Which.Should().Be("https://lyrics.test/api/search?track_name=S%C3%B6n%20g");
        response.Records.Select(record => record.Id).Should().Equal(1, 2);
    }

    [Test]
    public async Task SearchTextAsync_Text_SendsTheFreeTextQuery()
    {
        _handler.Respond(HttpStatusCode.OK, """[{"id":3,"plainLyrics":"A"}]""");

        LrclibResponse response = await _client.SearchTextAsync("Kerli Diamond Hard");

        _handler.Requests.Should().ContainSingle()
            .Which.Should().Be("https://lyrics.test/api/search?q=Kerli%20Diamond%20Hard");
        response.Records.Should().ContainSingle().Which.Id.Should().Be(3);
    }

    [Test]
    public async Task GetByIdAsync_Id_AsksForTheTrackWithTheId()
    {
        _handler.Respond(HttpStatusCode.OK, """{"id":42,"plainLyrics":"A"}""");

        LrclibResponse response = await _client.GetByIdAsync(42);

        _handler.Requests.Should().ContainSingle().Which.Should().Be("https://lyrics.test/api/get/42");
        response.Records.Should().ContainSingle().Which.Id.Should().Be(42);
    }

    [Test]
    public async Task PublishAsync_Challenge_SolvesItAndSendsTheLyricsWithTheToken()
    {
        _handler.Enqueue(HttpStatusCode.OK, $$"""{"prefix":"abc","target":"{{EasyTarget}}"}""");
        _handler.Enqueue(HttpStatusCode.Created, string.Empty);

        LrclibPublishResult result = await _client.PublishAsync(_publication);

        result.Status.Should().Be(LrclibPublishStatus.Published);
        _handler.Requests.Should().Equal(
            "https://lyrics.test/api/request-challenge",
            "https://lyrics.test/api/publish");
        string token = _handler.Tokens.Should().ContainSingle().Subject;
        token.Should().StartWith("abc:");
        long nonce = long.Parse(token["abc:".Length..], System.Globalization.CultureInfo.InvariantCulture);
        LrclibChallenge.IsSolution("abc", EasyTarget, nonce).Should().BeTrue();
        using var body = System.Text.Json.JsonDocument.Parse(_handler.Bodies[^1]);
        body.RootElement.GetProperty("trackName").GetString().Should().Be("Song");
        body.RootElement.GetProperty("artistName").GetString().Should().Be("Band");
        body.RootElement.GetProperty("albumName").GetString().Should().Be("Album");
        body.RootElement.GetProperty("duration").GetDouble().Should().Be(200);
        body.RootElement.GetProperty("plainLyrics").GetString().Should().Be("Line");
        body.RootElement.GetProperty("syncedLyrics").GetString().Should().Be("[00:01.00]Line");
    }

    [Test]
    public async Task PublishAsync_TokenRefused_IsRejected()
    {
        _handler.Enqueue(HttpStatusCode.OK, """{"prefix":"abc","target":"FF"}""");
        _handler.Enqueue(HttpStatusCode.BadRequest, """{"code":400,"name":"IncorrectPublishToken"}""");

        (await _client.PublishAsync(_publication)).Status.Should().Be(LrclibPublishStatus.Rejected);
    }

    [Test]
    public async Task PublishAsync_ChallengeRateLimitedOrBroken_PublishesNothing()
    {
        _handler.Enqueue(HttpStatusCode.TooManyRequests, string.Empty);
        (await _client.PublishAsync(_publication)).Status.Should().Be(LrclibPublishStatus.RateLimited);

        _handler.Enqueue(HttpStatusCode.OK, """{"prefix":"abc","target":"not hex"}""");
        (await _client.PublishAsync(_publication)).Status.Should().Be(LrclibPublishStatus.Unavailable);

        _handler.Requests.Should().NotContain("https://lyrics.test/api/publish");
    }

    [Test]
    public async Task SearchAsync_EmptyList_IsNotFound()
    {
        _handler.Respond(HttpStatusCode.OK, "[]");

        (await _client.SearchAsync(_query)).Status.Should().Be(LrclibStatus.NotFound);
    }

    [Test]
    public async Task GetAsync_NotFound_IsNotFound()
    {
        _handler.Respond(HttpStatusCode.NotFound, """{"code":404,"name":"TrackNotFound"}""");

        (await _client.GetAsync(_query)).Status.Should().Be(LrclibStatus.NotFound);
    }

    [Test]
    public async Task GetAsync_TooManyRequests_IsRateLimitedWithTheWaitOfTheService()
    {
        _handler.Respond(HttpStatusCode.TooManyRequests, string.Empty, retryAfterSeconds: 42);

        LrclibResponse response = await _client.GetAsync(_query);

        response.Status.Should().Be(LrclibStatus.RateLimited);
        response.RetryAfter.Should().Be(TimeSpan.FromSeconds(42));
    }

    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.BadGateway)]
    public async Task GetAsync_ServerError_IsUnavailable(HttpStatusCode status)
    {
        _handler.Respond(status, string.Empty);

        (await _client.GetAsync(_query)).Status.Should().Be(LrclibStatus.Unavailable);
    }

    [Test]
    public async Task GetAsync_NoConnection_IsUnavailable()
    {
        _handler.Fail(new HttpRequestException("No such host is known."));

        (await _client.GetAsync(_query)).Status.Should().Be(LrclibStatus.Unavailable);
    }

    [Test]
    public async Task GetAsync_Timeout_IsUnavailable()
    {
        _handler.Fail(new TaskCanceledException("Timeout"));

        (await _client.GetAsync(_query)).Status.Should().Be(LrclibStatus.Unavailable);
    }

    [Test]
    public async Task GetAsync_InvalidJson_IsUnavailable()
    {
        _handler.Respond(HttpStatusCode.OK, "<html>");

        (await _client.GetAsync(_query)).Status.Should().Be(LrclibStatus.Unavailable);
    }

    [Test]
    public async Task GetAsync_IncompleteQuery_Throws()
    {
        Func<Task> act = () => _client.GetAsync(_query with { AlbumName = null });

        await act.Should().ThrowAsync<ArgumentException>();
        _handler.Requests.Should().BeEmpty();
    }

    [Test]
    public void UserAgent_NamesTheApplication()
    {
        string[] products = [.. LrclibClient.UserAgent.Select(product => product.ToString())];

        products[0].Should().StartWith("WaveMP/");
        products.Should().Contain("(https://github.com/wavemp-app/wavemp)");
    }

    [Test]
    public void WaitTime_RequestsCloserThanTheInterval_WaitForTheRest()
    {
        DateTimeOffset now = DateTimeOffset.UnixEpoch.AddHours(1);
        var interval = TimeSpan.FromSeconds(1);

        LrclibRequestPacer.WaitTime(null, now, interval).Should().Be(TimeSpan.Zero);
        LrclibRequestPacer.WaitTime(now.AddMilliseconds(-300), now, interval)
            .Should().Be(TimeSpan.FromMilliseconds(700));
        LrclibRequestPacer.WaitTime(now.AddSeconds(-2), now, interval).Should().Be(TimeSpan.Zero);
    }

    /// <summary>
    /// Answers every request with the prepared response and records the requested URLs.
    /// </summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _queue = [];
        private Func<HttpResponseMessage> _respond = () => new HttpResponseMessage(HttpStatusCode.NotFound);

        public List<string> Requests { get; } = [];

        /// <summary>
        /// The publish tokens of the requests that sent one.
        /// </summary>
        public List<string> Tokens { get; } = [];

        /// <summary>
        /// The bodies of the requests that sent one.
        /// </summary>
        public List<string> Bodies { get; } = [];

        /// <summary>
        /// Answers the next request with the response; requests after the queued ones get the prepared response.
        /// </summary>
        public void Enqueue(HttpStatusCode status, string body)
        {
            _queue.Enqueue(() => new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }

        public void Respond(HttpStatusCode status, string body, int? retryAfterSeconds = null)
        {
            _respond = () =>
            {
                HttpResponseMessage response = new(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                };
                if (retryAfterSeconds is { } seconds)
                {
                    response.Headers.RetryAfter =
                        new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
                }

                return response;
            };
        }

        public void Fail(Exception exception)
        {
            _respond = () => throw exception;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            if (request.Headers.TryGetValues(LrclibClient.PublishTokenHeader, out IEnumerable<string>? tokens))
            {
                Tokens.AddRange(tokens);
            }

            if (request.Content is { } content)
            {
                Bodies.Add(await content.ReadAsStringAsync(cancellationToken));
            }

            return _queue.TryDequeue(out Func<HttpResponseMessage>? next) ? next() : _respond();
        }
    }
}
