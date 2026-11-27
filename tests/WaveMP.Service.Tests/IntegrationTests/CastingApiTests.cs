using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// The URLs a client hands to a casting device: the playback endpoints with the target Cast, the signed cover URL and
/// the CORS headers that let the receiver of the device read HLS streams and covers.
/// </summary>
[TestFixture]
[Category("Integration")]
public class CastingApiTests
{
    private const string CastFormats = "audioFormats=Mp3&audioFormats=Flac&target=Cast";
    private const string ReceiverOrigin = "https://receiver.example";

    private TemporaryDirectory _media = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _user = null!;
    private IReadOnlyList<TrackDto> _tracks = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Band/Album/01 - Covered.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = "Covered", Artist = "Band", Album = "Album" });
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Band/Album/cover.jpg"), 400, 400);
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Bare.flac"),
            AudioFormat.Flac,
            new TestTags { Title = "Bare" });
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Opus.opus"),
            AudioFormat.Opus,
            new TestTags { Title = "Opus" });
        await TestMediaGenerator.CreateVideoAsync(
            _media.Combine("Clip.mp4"),
            VideoFormat.Mp4,
            320,
            240,
            new TestTags { Title = "Clip" },
            TestVideoCodec.H264);

        _factory = new WaveMpWebApplicationFactory(
            "wavemp_tests_service_casting",
            freshDatabase: true,
            mediaPath: _media.Path);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        UserDto user = await _admin.CreateUserAsync(new { name = $"Caster {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        _user = _factory.CreateAuthorizedClient(session.Token);

        HttpResponseMessage settings = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/library",
            """{ "fileWatcherEnabled": false }""");
        settings.StatusCode.Should().Be(HttpStatusCode.OK, await settings.Content.ReadAsStringAsync());

        HttpResponseMessage response = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        ScanDto scan = await _admin.WaitForScanAsync(queued.Id);
        scan.Status.Should().Be(JobRunStatusDto.Succeeded);

        _tracks = (await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100")).Items;
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _user?.Dispose();
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
        _media.Dispose();
    }

    [Test]
    public async Task GetTrackPlayback_TargetCast_IssuesLongerStreamUrlAndSignedCover()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        TrackDto track = Track("Covered");

        TrackPlaybackDto playback = await PlaybackAsync("Covered", CastFormats);

        playback.Method.Should().Be(PlaybackMethodDto.DirectPlay);
        playback.StreamUrl.Should().StartWith($"/api/v1/tracks/{track.Id}/stream?uid=");
        // Six hours of buffer plus the length of the song.
        playback.ExpiresAt.Should().BeAfter(before.AddHours(6)).And.BeBefore(before.AddHours(6).AddMinutes(5));
        playback.CoverUrl.Should().StartWith($"/api/v1/tracks/{track.Id}/cover?size=1000&uid=");
        playback.CoverUrl.Should().Contain("&sid=").And.Contain("&exp=").And.Contain("&sig=");
    }

    [Test]
    public async Task GetTrackPlayback_DefaultTarget_KeepsTheShortLifetimeWithoutCover()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;

        TrackPlaybackDto playback = await PlaybackAsync("Covered", "audioFormats=Mp3");

        playback.ExpiresAt.Should().BeBefore(before.AddHours(2));
        playback.CoverUrl.Should().BeNull();
    }

    [Test]
    public async Task GetTrackPlayback_TargetLocal_HasNoCover()
    {
        TrackPlaybackDto playback = await PlaybackAsync("Covered", "audioFormats=Mp3&target=Local");

        playback.CoverUrl.Should().BeNull();
    }

    [Test]
    public async Task GetTrackPlayback_UnknownTarget_ReturnsValidationProblem()
    {
        HttpResponseMessage response = await _user.GetAsync(PlaybackPath("Covered", "audioFormats=Mp3&target=Tv"));

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Field.Should().Be("target");
    }

    [Test]
    public async Task GetTrackPlayback_UnsupportedFormatOfTheDevice_ReturnsHlsWithTheCastLifetime()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;

        TrackPlaybackDto playback = await PlaybackAsync("Opus", CastFormats);

        playback.Method.Should().Be(PlaybackMethodDto.Transcode);
        playback.StreamUrl.Should().Contain("/hls/master.m3u8?media=Audio&");
        playback.ExpiresAt.Should().BeAfter(before.AddHours(6));
    }

    [Test]
    public async Task HlsMasterPlaylist_CastUrl_KeepsItsSignatureInTheMediaPlaylist()
    {
        TrackPlaybackDto playback = await PlaybackAsync("Opus", CastFormats);
        string expiry = QueryValue(playback.StreamUrl!, "exp");

        HttpResponseMessage response = await _anonymous.GetAsync(playback.StreamUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadAsStringAsync()).Should().Contain($"exp={expiry}&");
    }

    [Test]
    public async Task CoverUrl_WithoutToken_ReturnsThePicture()
    {
        TrackPlaybackDto playback = await PlaybackAsync("Covered", CastFormats);

        HttpResponseMessage response = await _anonymous.GetAsync(playback.CoverUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
    }

    [Test]
    public async Task CoverUrl_TrackWithoutCover_ReturnsNotFound()
    {
        TrackPlaybackDto playback = await PlaybackAsync("Bare", CastFormats);

        HttpResponseMessage response = await _anonymous.GetAsync(playback.CoverUrl);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "cover.not_found");
    }

    [Test]
    public async Task CoverUrl_SignatureOnTheStream_IsNotAccepted()
    {
        TrackPlaybackDto playback = await PlaybackAsync("Covered", CastFormats);
        string query = playback.CoverUrl![playback.CoverUrl!.IndexOf("&uid=", StringComparison.Ordinal)..];

        HttpResponseMessage response = await _anonymous.GetAsync(
            $"/api/v1/tracks/{Track("Covered").Id}/stream?{query[1..]}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task StreamUrl_SignatureOnTheCover_IsNotAccepted()
    {
        TrackPlaybackDto playback = await PlaybackAsync("Covered", CastFormats);
        string query = playback.StreamUrl![(playback.StreamUrl!.IndexOf('?', StringComparison.Ordinal) + 1)..];

        HttpResponseMessage response = await _anonymous.GetAsync(
            $"/api/v1/tracks/{Track("Covered").Id}/cover?size=1000&{query}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task GetTrackCover_SessionToken_ReturnsThePicture()
    {
        HttpResponseMessage response = await _user.GetAsync($"/api/v1/tracks/{Track("Covered").Id}/cover?size=300");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task GetTrackVideoPlayback_TargetCast_IssuesLongerStreamUrlAndSignedCover()
    {
        DateTimeOffset before = DateTimeOffset.UtcNow;
        TrackDto track = Track("Clip");

        TrackVideoPlaybackDto playback = await _user.GetJsonAsync<TrackVideoPlaybackDto>(
            $"/api/v1/tracks/{track.Id}/video/playback?videoContainers=Mp4&videoCodecs=H264&audioCodecs=Aac"
            + "&target=Cast");

        playback.Method.Should().Be(PlaybackMethodDto.DirectPlay);
        playback.StreamUrl.Should().StartWith($"/api/v1/tracks/{track.Id}/video?uid=");
        playback.ExpiresAt.Should().BeAfter(before.AddHours(6));
        playback.CoverUrl.Should().StartWith($"/api/v1/tracks/{track.Id}/cover?size=1000&uid=");
    }

    [Test]
    public async Task GetTrackVideoPlayback_DefaultTarget_HasNoCover()
    {
        TrackVideoPlaybackDto playback = await _user.GetJsonAsync<TrackVideoPlaybackDto>(
            $"/api/v1/tracks/{Track("Clip").Id}/video/playback?videoContainers=Mp4&videoCodecs=H264&audioCodecs=Aac");

        playback.CoverUrl.Should().BeNull();
    }

    [Test]
    public async Task SignedStream_FromAnotherOrigin_AllowsAnyOrigin()
    {
        TrackPlaybackDto playback = await PlaybackAsync("Covered", CastFormats);
        using HttpRequestMessage request = new(HttpMethod.Get, playback.StreamUrl);
        request.Headers.Add("Origin", ReceiverOrigin);

        HttpResponseMessage response = await _anonymous.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("*");
        response.Headers.GetValues("Access-Control-Expose-Headers").Single().Should().Contain("Content-Range");
    }

    [Test]
    public async Task SignedStream_Preflight_AllowsGetWithRange()
    {
        TrackPlaybackDto playback = await PlaybackAsync("Covered", CastFormats);
        using HttpRequestMessage request = new(HttpMethod.Options, playback.StreamUrl);
        request.Headers.Add("Origin", ReceiverOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "range");

        HttpResponseMessage response = await _anonymous.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("*");
        response.Headers.GetValues("Access-Control-Allow-Headers").Single().Should().ContainEquivalentOf("range");
    }

    [Test]
    public async Task SignedStream_PreflightWithAuthorization_DoesNotAllowTheHeader()
    {
        using HttpRequestMessage request = new(
            HttpMethod.Options,
            $"/api/v1/tracks/{Track("Covered").Id}/stream");
        request.Headers.Add("Origin", ReceiverOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");

        HttpResponseMessage response = await _anonymous.SendAsync(request);

        // The browser blocks the request, because the answer does not allow the Authorization header.
        response.Headers.TryGetValues("Access-Control-Allow-Headers", out IEnumerable<string>? allowed);
        (allowed ?? []).Should().NotContain(
            value => value.Contains("authorization", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public async Task PlaybackEndpoint_FromAnotherOrigin_HasNoCorsHeaders()
    {
        using HttpRequestMessage request = new(HttpMethod.Get, PlaybackPath("Covered", CastFormats));
        request.Headers.Add("Origin", ReceiverOrigin);
        request.Headers.Authorization = _user.DefaultRequestHeaders.Authorization;

        HttpResponseMessage response = await _anonymous.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    private TrackDto Track(string title)
    {
        return _tracks.Single(track => track.Title == title);
    }

    private string PlaybackPath(string title, string query)
    {
        return $"/api/v1/tracks/{Track(title).Id}/playback?{query}";
    }

    private async Task<TrackPlaybackDto> PlaybackAsync(string title, string query)
    {
        return await _user.GetJsonAsync<TrackPlaybackDto>(PlaybackPath(title, query));
    }

    private static string QueryValue(string url, string name)
    {
        string query = url[(url.IndexOf('?', StringComparison.Ordinal) + 1)..];
        return query.Split('&')
            .Select(pair => pair.Split('=', 2))
            .Single(pair => pair[0] == name)[1];
    }
}
