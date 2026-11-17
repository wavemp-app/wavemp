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
            "https://lyrics.test/api/get?track_name=S%C3%B6n%20g&artist_name=Band%20%26%20Co&album_name=Album&duration=200");
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
        private Func<HttpResponseMessage> _respond = () => new HttpResponseMessage(HttpStatusCode.NotFound);

        public List<string> Requests { get; } = [];

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

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!.AbsoluteUri);
            return Task.FromResult(_respond());
        }
    }
}
