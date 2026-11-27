using System.Net;
using System.Net.Http.Headers;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Core.OnlineMetadata.Options;
using WaveMP.Core.OnlineMetadata.Tests.Fakes;
using WaveMP.Core.Settings.Groups;

namespace WaveMP.Core.OnlineMetadata.Tests.Clients;

/// <summary>
/// The requests of the artist picture lookup against a simulated service with simulated time: their own switch, the
/// host allowlist with manually followed redirects, the waits of Wikimedia and the size limits. No request leaves the
/// process.
/// </summary>
[TestFixture]
public class ArtistLookupSenderTests
{
    private static readonly Uri _commons = new("https://commons.wikimedia.org/w/api.php?action=query");
    private static readonly Uri _album = new("https://covers.test/release/1/front-500");

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

    [Test]
    public async Task SendAsync_ArtistLookupOff_SendsNoRequestWhileTheAlbumSearchStillWorks()
    {
        _clients.ArtistSettings.Current = new ArtistPictureSettings { OnlineLookupEnabled = false };
        _clients.Handler.Respond(HttpStatusCode.OK, "answer");

        OnlineResponse<byte[]> artist = await SendAsync(_commons);
        OnlineResponse<byte[]> album = await _clients.Sender.GetAsync(_album, false, 1024, CancellationToken.None);

        artist.Status.Should().Be(OnlineStatus.Disabled);
        album.IsFound.Should().BeTrue();
        _clients.Handler.Requests.Should().ContainSingle().Which.Url.Should().Be(_album.AbsoluteUri);
    }

    [Test]
    public async Task SendAsync_AlbumSearchOff_DoesNotStopTheArtistLookup()
    {
        _clients.Settings.Current = new OnlineMetadataSettings { Enabled = false };
        _clients.Handler.Respond(HttpStatusCode.OK, "answer");

        OnlineResponse<byte[]> artist = await SendAsync(_commons);
        OnlineResponse<byte[]> album = await _clients.Sender.GetAsync(_album, false, 1024, CancellationToken.None);

        artist.IsFound.Should().BeTrue();
        album.Status.Should().Be(OnlineStatus.Disabled);
        _clients.Handler.Requests.Should().ContainSingle().Which.Url.Should().Be(_commons.AbsoluteUri);
    }

    [Test]
    public async Task Clients_ArtistLookupOff_SendNoRequestAtAll()
    {
        _clients.ArtistSettings.Current = new ArtistPictureSettings
        {
            OnlineLookupEnabled = false,
            FanartTvApiKey = "key",
        };
        var artist = Guid.NewGuid();
        Uri picture = new("https://upload.wikimedia.org/picture.jpg");

        OnlineStatus[] statuses =
        [
            (await _clients.MusicBrainzArtists.SearchAsync("Band", 0, 10)).Status,
            (await _clients.MusicBrainzArtists.GetArtistAsync(artist)).Status,
            (await _clients.Wikidata.GetImageFileNamesAsync("Q1")).Status,
            (await _clients.Commons.GetImagesAsync(["Picture.jpg"], 500)).Status,
            (await _clients.Commons.DownloadAsync(picture)).Status,
            (await _clients.FanartTv.GetArtistPicturesAsync(artist, "key")).Status,
            (await _clients.FanartTv.DownloadAsync(new Uri("https://assets.fanart.tv/picture.jpg"))).Status,
        ];

        statuses.Should().AllSatisfy(status => status.Should().Be(OnlineStatus.Disabled));
        _clients.Handler.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task SendAsync_AddressNotAllowed_IsNeverSent()
    {
        OnlineResponse<byte[]> response = await SendAsync(new Uri("https://intranet.local/secret"));

        response.Status.Should().Be(OnlineStatus.Unavailable);
        _clients.Handler.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task SendAsync_RedirectToAnAllowedHost_FollowsIt()
    {
        _clients.Handler
            .Respond(HttpStatusCode.Found, configure: Redirect("https://upload.wikimedia.org/picture.jpg"))
            .Respond(HttpStatusCode.OK, "picture");

        OnlineResponse<byte[]> response = await SendAsync(_commons);

        response.IsFound.Should().BeTrue();
        _clients.Handler.Requests.Select(request => request.Url).Should().Equal(
            _commons.AbsoluteUri,
            "https://upload.wikimedia.org/picture.jpg");
    }

    [Test]
    public async Task SendAsync_RelativeRedirect_IsResolvedAgainstTheAddress()
    {
        _clients.Handler
            .Respond(HttpStatusCode.MovedPermanently, configure: Redirect("/wiki/File:Picture.jpg"))
            .Respond(HttpStatusCode.OK, "page");

        OnlineResponse<byte[]> response = await SendAsync(_commons);

        response.IsFound.Should().BeTrue();
        _clients.Handler.Requests[1].Url.Should().Be("https://commons.wikimedia.org/wiki/File:Picture.jpg");
    }

    [TestCase("https://evil.example/picture.jpg")]
    [TestCase("http://upload.wikimedia.org/picture.jpg")]
    [TestCase("https://127.0.0.1/picture.jpg")]
    [TestCase("https://upload.wikimedia.org:444/picture.jpg")]
    public async Task SendAsync_RedirectToAnAddressNotAllowed_IsRefused(string target)
    {
        _clients.Handler
            .Respond(HttpStatusCode.TemporaryRedirect, configure: Redirect(target))
            .Respond(HttpStatusCode.OK, "secret");

        OnlineResponse<byte[]> response = await SendAsync(_commons);

        response.Status.Should().Be(OnlineStatus.Unavailable);
        _clients.Handler.Requests.Should().ContainSingle().Which.Url.Should().Be(_commons.AbsoluteUri);
    }

    [Test]
    public async Task SendAsync_MoreThanThreeRedirects_GivesUp()
    {
        _clients.Handler.Respond(HttpStatusCode.Found, configure: Redirect("https://upload.wikimedia.org/again"));

        OnlineResponse<byte[]> response = await SendAsync(_commons);

        response.Status.Should().Be(OnlineStatus.Unavailable);
        _clients.Handler.Requests.Should().HaveCount(OnlineRequestSender.MaxRedirects + 1);
    }

    [Test]
    public async Task SendAsync_SwitchedOffDuringARedirect_SendsNoFurtherRequest()
    {
        _clients.Handler.Respond(
            HttpStatusCode.Found,
            configure: response =>
            {
                Redirect("https://upload.wikimedia.org/picture.jpg")(response);
                _clients.ArtistSettings.Current = new ArtistPictureSettings { OnlineLookupEnabled = false };
            });

        OnlineResponse<byte[]> response = await SendAsync(_commons);

        response.Status.Should().Be(OnlineStatus.Disabled);
        _clients.Handler.Requests.Should().ContainSingle();
    }

    [Test]
    public async Task SendAsync_HeadersOfTheRequest_GoToItsHostOnlyNeverToARedirect()
    {
        _clients.Handler
            .Respond(HttpStatusCode.Found, configure: Redirect("https://assets.fanart.tv/picture.jpg"))
            .Respond(HttpStatusCode.OK, "picture");
        Uri service = new("https://webservice.fanart.tv/v3/music/x");

        await _clients.Sender.SendAsync(
            new OnlineRequest(service, OnlineGate.ArtistPictures, 1024)
            {
                Headers = [new KeyValuePair<string, string>("api-key", "secret-key")],
            },
            CancellationToken.None);

        _clients.Handler.Requests[0].Headers.Should().Contain("api-key", "secret-key");
        _clients.Handler.Requests[1].Headers.Should().NotContainKey("api-key");
        _clients.Handler.Requests.Should().AllSatisfy(request => request.Url.Should().NotContain("secret-key"));
    }

    [Test]
    public async Task SendAsync_TooManyRequestsWithoutRetryAfter_WaitsAtLeastTheMinimumOfWikimedia()
    {
        _clients.Handler
            .Respond(HttpStatusCode.TooManyRequests)
            .Respond(HttpStatusCode.OK, "late");

        OnlineResponse<byte[]> response = await SendAsync(_commons, TimeSpan.FromSeconds(5));

        response.IsFound.Should().BeTrue();
        _clients.Clock.Delays.Should().Equal(TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task SendAsync_BusyWithRetryAfter_WaitsTheNamedTime()
    {
        _clients.Handler
            .Respond(
                HttpStatusCode.ServiceUnavailable,
                configure: response =>
                    response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(7)))
            .Respond(HttpStatusCode.OK, "late");

        OnlineResponse<byte[]> response = await SendAsync(_commons, TimeSpan.FromSeconds(5));

        response.IsFound.Should().BeTrue();
        _clients.Clock.Delays.Should().Equal(TimeSpan.FromSeconds(7));
    }

    [Test]
    public async Task SendAsync_RetryAfterLongerThanTheLongestWait_GivesUpInsteadOfAskingEarlier()
    {
        _clients.Handler.Respond(
            HttpStatusCode.TooManyRequests,
            configure: response =>
                response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromMinutes(5)));

        OnlineResponse<byte[]> response = await SendAsync(_commons, TimeSpan.FromSeconds(5));

        response.Status.Should().Be(OnlineStatus.Unavailable);
        _clients.Handler.Requests.Should().ContainSingle();
        _clients.Clock.Delays.Should().BeEmpty();
    }

    [Test]
    public async Task SendAsync_AnswerLargerThanTheLimit_IsUnavailable()
    {
        _clients.Handler.Respond(HttpStatusCode.OK, new byte[2048]);

        OnlineResponse<byte[]> response = await _clients.Sender.SendAsync(
            new OnlineRequest(_commons, OnlineGate.ArtistPictures, 1024),
            CancellationToken.None);

        response.Status.Should().Be(OnlineStatus.Unavailable);
    }

    [Test]
    public async Task SendAsync_ContentLengthLargerThanTheLimit_IsUnavailableWithoutReading()
    {
        _clients.Handler.Respond(
            HttpStatusCode.OK,
            new byte[10],
            response => response.Content.Headers.ContentLength = 1_000_000);

        OnlineResponse<byte[]> response = await _clients.Sender.SendAsync(
            new OnlineRequest(_commons, OnlineGate.ArtistPictures, 1024),
            CancellationToken.None);

        response.Status.Should().Be(OnlineStatus.Unavailable);
    }

    [Test]
    public async Task SendAsync_ConfiguredMirror_IsAllowedWithItsScheme()
    {
        _clients.Settings.Current = new OnlineMetadataSettings
        {
            Enabled = false,
            MusicBrainzUrl = "http://mirror.local:5000",
        };
        _clients.Handler.Respond(HttpStatusCode.OK, """{ "count": 0, "artists": [] }""");

        OnlineResponse<MusicBrainzArtistPage> response = await _clients.MusicBrainzArtists.SearchAsync("Band", 0, 10);

        response.IsFound.Should().BeTrue();
        _clients.Handler.Requests.Should().ContainSingle()
            .Which.Url.Should().StartWith("http://mirror.local:5000/ws/2/artist?");
    }

    [Test]
    public async Task SendAsync_SameLane_SendsOneRequestAfterTheOther()
    {
        using GatedHandler handler = new();
        using HttpClient http = new(handler);
        SimulatedClock clock = new();
        FakeSnapshot<OnlineMetadataSettings> settings = new(new OnlineMetadataSettings());
        OnlineRequestSender sender = new(
            http,
            http,
            new MusicBrainzRequestQueue(clock, settings),
            settings,
            new FakeSnapshot<ArtistPictureSettings>(new ArtistPictureSettings { OnlineLookupEnabled = true }),
            Microsoft.Extensions.Options.Options.Create(new OnlineMetadataOptions()),
            Microsoft.Extensions.Options.Options.Create(new ArtistLookupClientOptions()),
            clock,
            NullLogger<OnlineRequestSender>.Instance);
        OnlineRequest request = new(_commons, OnlineGate.ArtistPictures, 1024) { Lane = OnlineLane.Wikimedia };
        OnlineRequest other = request with { Lane = OnlineLane.FanartTv, Url = new Uri("https://assets.fanart.tv/a") };

        Task<OnlineResponse<byte[]>> first = sender.SendAsync(request, CancellationToken.None);
        Task<OnlineResponse<byte[]>> second = sender.SendAsync(request, CancellationToken.None);
        Task<OnlineResponse<byte[]>> fanart = sender.SendAsync(other, CancellationToken.None);
        await handler.WaitForAsync(2);

        // The second request of Wikimedia waits for the first; fanart.tv has a lane of its own.
        handler.Count.Should().Be(2);
        handler.Release();
        await Task.WhenAll(first, second, fanart);
        handler.Count.Should().Be(3);
    }

    private Task<OnlineResponse<byte[]>> SendAsync(Uri url, TimeSpan? minRetryDelay = null)
    {
        return _clients.Sender.SendAsync(
            new OnlineRequest(url, OnlineGate.ArtistPictures, 1024)
            {
                Lane = OnlineLane.Wikimedia,
                MinRetryDelay = minRetryDelay ?? TimeSpan.Zero,
            },
            CancellationToken.None);
    }

    private static Action<HttpResponseMessage> Redirect(string target)
    {
        return response => response.Headers.Location = new Uri(target, UriKind.RelativeOrAbsolute);
    }

    /// <summary>
    /// Holds every answer until the test releases them, and counts the requests that arrived.
    /// </summary>
    private sealed class GatedHandler : HttpMessageHandler
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _count;

        public int Count => Volatile.Read(ref _count);

        public void Release()
        {
            _release.TrySetResult();
        }

        public async Task WaitForAsync(int count)
        {
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(10));
            while (Count < count)
            {
                await Task.Delay(10, timeout.Token);
            }

            // Give a request that should wait the chance to arrive anyway.
            await Task.Delay(100, timeout.Token);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _count);
            await _release.Task.WaitAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1]) };
        }
    }
}
