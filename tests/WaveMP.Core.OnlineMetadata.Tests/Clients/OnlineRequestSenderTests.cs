using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.OnlineMetadata.Tests.Fakes;
using WaveMP.Core.Settings.Groups;

namespace WaveMP.Core.OnlineMetadata.Tests.Clients;

/// <summary>
/// Runs the sender against a simulated service with simulated time; no request leaves the process.
/// </summary>
[TestFixture]
public class OnlineRequestSenderTests
{
    private static readonly Uri _url = new("https://covers.test/release/1/front-500");

    private OnlineClients _clients = null!;

    [SetUp]
    public void SetUp()
    {
        _clients = new OnlineClients();
    }

    [TearDown]
    public void TearDown()
    {
        _clients.Dispose();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetAsync_Disabled_SendsNoRequest(bool queued)
    {
        _clients.Settings.Current = new OnlineMetadataSettings { Enabled = false };

        OnlineResponse<byte[]> response = await _clients.Sender.GetAsync(_url, queued, 1024, CancellationToken.None);

        response.Status.Should().Be(OnlineStatus.Disabled);
        _clients.Handler.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task GetAsync_Found_ReadsContentAndSendsUserAgent()
    {
        _clients.Handler.Respond(HttpStatusCode.OK, "answer");

        OnlineResponse<byte[]> response = await _clients.Sender.GetAsync(_url, false, 1024, CancellationToken.None);

        response.IsFound.Should().BeTrue();
        System.Text.Encoding.UTF8.GetString(response.Value!).Should().Be("answer");
        _clients.Handler.Requests.Should().ContainSingle()
            .Which.UserAgent.Should().Be(OnlineUserAgent.Value);
    }

    [Test]
    public void UserAgent_NamesProductVersionAndContact()
    {
        OnlineUserAgent.Value.Should().MatchRegex(@"^WaveMP/[0-9][^\s]* \( https://github\.com/wavemp-app/wavemp \)$");
    }

    [Test]
    public async Task GetAsync_NotFound_IsNotFoundWithoutRetry()
    {
        _clients.Handler.Respond(HttpStatusCode.NotFound);

        OnlineResponse<byte[]> response = await _clients.Sender.GetAsync(_url, false, 1024, CancellationToken.None);

        response.Status.Should().Be(OnlineStatus.NotFound);
        _clients.Handler.Requests.Should().ContainSingle();
    }

    [Test]
    public async Task GetAsync_ServerError_IsUnavailableWithoutRetry()
    {
        _clients.Handler.Respond(HttpStatusCode.InternalServerError);

        OnlineResponse<byte[]> response = await _clients.Sender.GetAsync(_url, false, 1024, CancellationToken.None);

        response.Status.Should().Be(OnlineStatus.Unavailable);
        _clients.Handler.Requests.Should().ContainSingle();
    }

    [Test]
    public async Task GetAsync_ServiceUnavailable_TriesAgainWithGrowingBackoff()
    {
        _clients.Handler
            .Respond(HttpStatusCode.ServiceUnavailable)
            .Respond(HttpStatusCode.ServiceUnavailable)
            .Respond(HttpStatusCode.OK, "late");

        OnlineResponse<byte[]> response = await _clients.Sender.GetAsync(_url, false, 1024, CancellationToken.None);

        response.IsFound.Should().BeTrue();
        _clients.Clock.Delays.Should().Equal(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2));
        DateTimeOffset first = _clients.Handler.Requests[0].SentAt;
        _clients.Handler.Requests.Select(request => request.SentAt - first)
            .Should().Equal(TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3));
    }

    [Test]
    public async Task GetAsync_ServiceUnavailableWithRetryAfter_WaitsTheNamedTime()
    {
        _clients.Handler
            .Respond(
                HttpStatusCode.ServiceUnavailable,
                configure: response =>
                    response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(4)))
            .Respond(HttpStatusCode.OK, "late");

        OnlineResponse<byte[]> response = await _clients.Sender.GetAsync(_url, false, 1024, CancellationToken.None);

        response.IsFound.Should().BeTrue();
        _clients.Clock.Delays.Should().Equal(TimeSpan.FromSeconds(4));
    }

    [Test]
    public async Task GetAsync_ServiceUnavailableEachTime_GivesUpAfterTheLastAttempt()
    {
        _clients.Handler.Respond(HttpStatusCode.ServiceUnavailable);

        OnlineResponse<byte[]> response = await _clients.Sender.GetAsync(_url, false, 1024, CancellationToken.None);

        response.Status.Should().Be(OnlineStatus.Unavailable);
        _clients.Handler.Requests.Should().HaveCount(_clients.Options.MaxAttempts);
    }

    [Test]
    public async Task GetAsync_SwitchedOffWhileWaitingForRetry_SendsNoFurtherRequest()
    {
        _clients.Handler.Respond(
            HttpStatusCode.ServiceUnavailable,
            configure: _ => _clients.Settings.Current = new OnlineMetadataSettings { Enabled = false });

        OnlineResponse<byte[]> response = await _clients.Sender.GetAsync(_url, false, 1024, CancellationToken.None);

        response.Status.Should().Be(OnlineStatus.Disabled);
        _clients.Handler.Requests.Should().ContainSingle();
    }

    [Test]
    public async Task GetAsync_NetworkError_IsUnavailable()
    {
        _clients.Handler.Fail(new HttpRequestException("No route to host"));

        OnlineResponse<byte[]> response = await _clients.Sender.GetAsync(_url, false, 1024, CancellationToken.None);

        response.Status.Should().Be(OnlineStatus.Unavailable);
    }

    [Test]
    public async Task GetAsync_AnswerLargerThanLimit_IsUnavailable()
    {
        _clients.Handler.Respond(HttpStatusCode.OK, new byte[2048]);

        OnlineResponse<byte[]> response = await _clients.Sender.GetAsync(_url, false, 1024, CancellationToken.None);

        response.Status.Should().Be(OnlineStatus.Unavailable);
    }

    [TestCase(1, null, 1)]
    [TestCase(2, null, 2)]
    [TestCase(3, null, 4)]
    [TestCase(6, null, 10)]
    [TestCase(1, 7, 7)]
    [TestCase(1, 60, 10)]
    public void RetryDelay_GrowsAndKeepsTheLimit(int attempt, int? retryAfterSeconds, int expectedSeconds)
    {
        TimeSpan? retryAfter = retryAfterSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null;

        TimeSpan delay = OnlineRequestSender.RetryDelay(
            attempt,
            retryAfter,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(10));

        delay.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }
}
