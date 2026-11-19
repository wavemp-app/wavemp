using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Chooses the songs of autoplay through the API, with a small library of synthetic media: three artists, two genres
/// and a music video without audio file.
/// </summary>
[TestFixture]
[Category("Integration")]
public class AutoplayApiTests
{
    private const string Path = "/api/v1/me/autoplay/next";

    private TemporaryDirectory _media = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private IReadOnlyList<TrackDto> _tracks = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        (string Path, string Title, string Artist, string Genre, string Year)[] songs =
        [
            ("Alpha/First Album/01 - One.mp3", "One", "Alpha", "Rock", "2001"),
            ("Alpha/First Album/02 - Two.mp3", "Two", "Alpha", "Rock", "2001"),
            ("Alpha/First Album/03 - Three.mp3", "Three", "Alpha", "Rock", "2001"),
            ("Alpha/Second Album/01 - Four.mp3", "Four", "Alpha", "Rock", "2003"),
            ("Beta/Beta Album/01 - Neighbor.mp3", "Neighbor", "Beta", "rock", "2005"),
            ("Beta/Beta Album/02 - Old Neighbor.mp3", "Old Neighbor", "Beta", "Rock", "1975"),
            ("Gamma/Gamma Album/01 - Stranger.mp3", "Stranger", "Gamma", "Jazz", "2001"),
        ];
        foreach ((string path, string title, string artist, string genre, string year) in songs)
        {
            await TestMediaGenerator.CreateAudioAsync(
                _media.Combine(path),
                AudioFormat.Mp3,
                new TestTags { Title = title, Artist = artist, Genre = genre, Date = year });
        }

        await TestMediaGenerator.CreateVideoAsync(
            _media.Combine("Alpha/Clip.mp4"),
            VideoFormat.Mp4,
            160,
            120,
            new TestTags { Title = "Clip", Artist = "Alpha", Genre = "Rock", Date = "2001" });

        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_autoplay",
            freshDatabase: true,
            mediaPath: _media.Path);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();

        HttpResponseMessage response = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        ScanDto scan = await _admin.WaitForScanAsync(queued.Id);
        scan.Status.Should().Be(JobRunStatusDto.Succeeded);

        _tracks = (await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100")).Items;
        _tracks.Should().HaveCount(8);
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
    public async Task SelectMyAutoplayTracks_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _anonymous.PostAsJsonAsync(Path, Request("One"));

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task SelectMyAutoplayTracks_AlbumOfOneArtist_ContinuesWithTheArtistThenTheSameGenreAndDecade()
    {
        using HttpClient client = await CreateProfileAsync();

        AutoplayResultDto result = await SelectAsync(client, Request("One", "Two"));

        string[] titles = [.. result.Items.Select(item => item.Track.Title)];
        titles.Should().HaveCount(4);
        titles.Take(3).Should().BeEquivalentTo(["Three", "Four", "Clip"]);
        titles[3].Should().Be("Neighbor");
    }

    [Test]
    public async Task SelectMyAutoplayTracks_Always_ReturnsThePlaybackWithASignedStreamUrl()
    {
        using HttpClient client = await CreateProfileAsync();

        AutoplayResultDto result = await SelectAsync(client, Request("Three"));

        AutoplayTrackDto four = result.Items.Single(item => item.Track.Title == "Four");
        four.Track.Artist!.Name.Should().Be("Alpha");
        four.Playback.TrackId.Should().Be(four.Track.Id);
        four.Playback.Method.Should().Be(PlaybackMethodDto.DirectPlay);
        four.Playback.StreamUrl.Should().StartWith($"/api/v1/tracks/{four.Track.Id}/stream?").And.Contain("sig=");
        four.Playback.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);

        using HttpClient player = _factory.CreateClient();
        HttpResponseMessage stream = await player.GetAsync(four.Playback.StreamUrl);
        stream.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task SelectMyAutoplayTracks_VideoOnlySwitchedOff_LeavesOutSongsWithAMusicVideoOnly()
    {
        using HttpClient client = await CreateProfileAsync();
        (await client.PatchAsJsonAsync("/api/v1/me/settings", new { autoplay = new { includeVideoOnly = false } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        AutoplayResultDto result = await SelectAsync(client, Request("One", "Two"));

        result.Items.Select(item => item.Track.Title).Should().BeEquivalentTo(["Three", "Four", "Neighbor"]);
    }

    [Test]
    public async Task SelectMyAutoplayTracks_ExcludedSongsAndOneStar_AreNeverChosen()
    {
        using HttpClient client = await CreateProfileAsync();
        (await client.PutAsJsonAsync($"/api/v1/tracks/{Track("Four").Id}/rating", new { rating = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        AutoplayResultDto result = await SelectAsync(
            client,
            Request(["One", "Two"], excluded: [Track("Clip").Id]));

        result.Items.Select(item => item.Track.Title).Should().Equal("Three", "Neighbor");
    }

    [Test]
    public async Task SelectMyAutoplayTracks_RecentlyPlayedSong_ComesOnlyWhenNothingElseIsLeft()
    {
        using HttpClient client = await CreateProfileAsync();
        (await client.PostAsJsonAsync("/api/v1/me/history", new { trackId = Track("Three").Id }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        AutoplayResultDto fresh = await SelectAsync(client, Request("One", "Two"));
        AutoplayResultDto rested = await SelectAsync(
            client,
            Request(["One", "Two"], excluded: [Track("Four").Id, Track("Clip").Id, Track("Neighbor").Id]));

        fresh.Items.Select(item => item.Track.Title).Should().NotContain("Three");
        rested.Items.Select(item => item.Track.Title).Should().Equal("Three");
    }

    [Test]
    public async Task SelectMyAutoplayTracks_WithCount_ReturnsAtMostThatMany()
    {
        using HttpClient client = await CreateProfileAsync();

        AutoplayResultDto result = await SelectAsync(client, Request(["One"], count: 2));

        result.Items.Should().HaveCount(2);
        result.Items.Select(item => item.Track.Artist!.Name).Should().OnlyContain(name => name == "Alpha");
    }

    [Test]
    public async Task SelectMyAutoplayTracks_ArtistWithoutOtherSongs_ReturnsAnEmptyList()
    {
        using HttpClient client = await CreateProfileAsync();

        AutoplayResultDto result = await SelectAsync(client, Request("Stranger"));

        result.Items.Should().BeEmpty();
    }

    private static IEnumerable<TestCaseData> InvalidRequests()
    {
        yield return new TestCaseData("{\"trackIds\":[],\"audioFormats\":[\"Mp3\"]}", "trackIds", ValidationErrorCodeDto.MinItems)
            .SetArgDisplayNames("NoTracks");
        yield return new TestCaseData("{\"audioFormats\":[\"Mp3\"]}", "trackIds", ValidationErrorCodeDto.Required)
            .SetArgDisplayNames("MissingTracks");
        yield return new TestCaseData(
                $"{{\"trackIds\":[\"{Guid.Empty}\"]}}",
                "audioFormats",
                ValidationErrorCodeDto.Required)
            .SetArgDisplayNames("MissingFormats");
        yield return new TestCaseData(
                $"{{\"trackIds\":[\"{Guid.Empty}\"],\"audioFormats\":[\"Mp3\"],\"count\":51}}",
                "count",
                ValidationErrorCodeDto.OutOfRange)
            .SetArgDisplayNames("CountTooHigh");
        yield return new TestCaseData(
                $"{{\"trackIds\":[{string.Join(',', Enumerable.Repeat($"\"{Guid.Empty}\"", 501))}],\"audioFormats\":[\"Mp3\"]}}",
                "trackIds",
                ValidationErrorCodeDto.MaxItems)
            .SetArgDisplayNames("TooManyTracks");
    }

    [TestCaseSource(nameof(InvalidRequests))]
    public async Task SelectMyAutoplayTracks_InvalidRequest_ReturnsTheCode(
        string json,
        string field,
        ValidationErrorCodeDto code)
    {
        using HttpClient client = await CreateProfileAsync();
        using StringContent body = new(json, System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response = await client.PostAsync(Path, body);

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().Contain(error => error.Field == field && error.Code == code);
    }

    private async Task<HttpClient> CreateProfileAsync()
    {
        UserDto user = await _admin.CreateUserAsync(new { name = $"Autoplay {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        return _factory.CreateAuthorizedClient(session.Token);
    }

    private static async Task<AutoplayResultDto> SelectAsync(HttpClient client, object request)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(Path, request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        return (await response.Content.ReadFromJsonAsync<AutoplayResultDto>(HttpClientExtensions.JsonOptions))!;
    }

    private object Request(params string[] titles)
    {
        return Request(titles, excluded: null);
    }

    private object Request(string[] titles, Guid[]? excluded = null, int? count = null)
    {
        return new
        {
            trackIds = titles.Select(title => Track(title).Id).ToArray(),
            excludedTrackIds = excluded,
            count,
            audioFormats = new[] { "Mp3" },
        };
    }

    private TrackDto Track(string title)
    {
        return _tracks.Single(track => track.Title == title);
    }
}
