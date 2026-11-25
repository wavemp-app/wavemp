using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Lyrics.Downloads;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Reads and downloads lyrics of a library of synthetic media through the API. LRCLIB is simulated by a substitute;
/// no request leaves the process. The file watcher is off, so only scans and the API change the library.
/// </summary>
[TestFixture]
[Category("Integration")]
public class LyricsApiTests
{
    private const string EnhancedLrc = """
        [ar:Band]
        [00:00.20]<00:00.20>Hello <00:00.50>world
        [00:00.80]Second line
        """;

    private TemporaryDirectory _media = null!;
    private ILrclibClient _lrclib = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _user = null!;
    private IReadOnlyList<TrackDto> _tracks = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        Directory.CreateDirectory(_media.Combine("Band/Album"));
        await AudioAsync("Synced");
        await File.WriteAllTextAsync(_media.Combine("Band/Album/Synced.lrc"), EnhancedLrc);
        await AudioAsync("Tagged", lyrics: "First tag line\nSecond tag line");
        await AudioAsync("Tagged Busy", lyrics: "Tag lyrics");
        await AudioAsync("Losing", lyrics: "Lyrics that leave the tags");
        await AudioAsync("Plain");
        await AudioAsync("Missing");
        await AudioAsync("Instrumental");

        _lrclib = Substitute.For<ILrclibClient>();
        _lrclib.GetAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>()).Returns(LrclibResponse.NotFound);
        _lrclib.SearchAsync(Arg.Any<LrclibQuery>(), Arg.Any<CancellationToken>()).Returns(LrclibResponse.NotFound);
        Answer("Plain", LrclibResponse.Found([new LrclibRecord
        {
            Id = 1,
            Duration = 1,
            PlainLyrics = "Plain line",
            SyncedLyrics = "[00:00.10]Downloaded line",
        }]));
        Answer("Instrumental", LrclibResponse.Found([new LrclibRecord { Id = 2, Duration = 1, Instrumental = true }]));
        Answer("Losing", LrclibResponse.Found([new LrclibRecord
        {
            Id = 3,
            Duration = 1,
            SyncedLyrics = "[00:00.10]Line from LRCLIB",
        }]));
        Answer("Tagged", LrclibResponse.Unavailable);
        Answer("Tagged Busy", LrclibResponse.RateLimited(TimeSpan.FromSeconds(30)));

        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_lyrics",
            freshDatabase: true,
            mediaPath: _media.Path,
            settings: new Dictionary<string, string?>
            {
                ["Lyrics:RequestInterval"] = "00:00:00",
                ["Lyrics:MaxRetryDelay"] = "00:00:00",
            },
            lrclib: _lrclib);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        UserDto user = await _admin.CreateUserAsync(new { name = $"Listener {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        _user = _factory.CreateAuthorizedClient(session.Token);

        await PatchSettingsAsync("""{ "fileWatcherEnabled": false }""");
        HttpResponseMessage response = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        ScanDto scan = await _admin.WaitForScanAsync(queued.Id);
        scan.Status.Should().Be(JobRunStatusDto.Succeeded);
        await _admin.WaitForJobsIdleAsync();

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
    public async Task GetTrackLyrics_WithoutToken_IsRejected()
    {
        await (await _anonymous.GetAsync($"/api/v1/tracks/{Track("Synced").Id}/lyrics"))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task GetTrackLyrics_LyricsFile_ReturnsTheWordSynchronizedLines()
    {
        LyricsDto lyrics = await _user.GetJsonAsync<LyricsDto>($"/api/v1/tracks/{Track("Synced").Id}/lyrics");

        lyrics.Type.Should().Be(LyricsTypeDto.WordSynchronized);
        lyrics.Source.Should().Be(LyricsSourceDto.File);
        lyrics.Lines.Select(line => (line.StartMilliseconds, line.EndMilliseconds, line.Text)).Should().Equal(
            (200L, 800L, "Hello world"),
            (800L, (long?)null, "Second line"));
        lyrics.Lines[0].Words.Select(word => (word.StartMilliseconds, word.EndMilliseconds, word.Text)).Should().Equal(
            (200L, 500L, "Hello "),
            (500L, 800L, "world"));
        lyrics.Lines[1].Words.Should().BeEmpty();
    }

    [Test]
    public async Task ListTrackFiles_LyricsFile_BelongsToTheTrack()
    {
        PageDto<MediaFileDto> files = await _admin.GetJsonAsync<PageDto<MediaFileDto>>(
            $"/api/v1/tracks/{Track("Synced").Id}/files");

        files.Items.Select(file => (file.FileName, file.Kind)).Should().Equal(
            ("Synced.mp3", MediaFileKindDto.Audio),
            ("Synced.lrc", MediaFileKindDto.Lyrics));
    }

    [Test]
    public async Task GetTrackLyrics_TagLyrics_ReturnsStaticLines()
    {
        LyricsDto lyrics = await _user.GetJsonAsync<LyricsDto>($"/api/v1/tracks/{Track("Tagged").Id}/lyrics");

        lyrics.Type.Should().Be(LyricsTypeDto.Static);
        lyrics.Source.Should().Be(LyricsSourceDto.Tags);
        lyrics.Lines.Select(line => line.Text).Should().Equal("First tag line", "Second tag line");
        lyrics.Lines.Should().AllSatisfy(line => line.StartMilliseconds.Should().BeNull());
    }

    [Test]
    public async Task GetTrackLyrics_NoLyricsOrUnknownTrack_ReturnsNotFound()
    {
        await (await _user.GetAsync($"/api/v1/tracks/{Track("Missing").Id}/lyrics"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "lyrics.not_found");
        await (await _user.GetAsync($"/api/v1/tracks/{Guid.NewGuid()}/lyrics"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "track.not_found");
    }

    [Test]
    public async Task DownloadTrackLyrics_WithoutTokenOrAsUser_IsRejected()
    {
        string path = $"/api/v1/tracks/{Track("Missing").Id}/lyrics/download";

        await (await _anonymous.PostAsync(path, null))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await (await _user.PostAsync(path, null))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Test]
    public async Task DownloadTrackLyrics_SwitchedOff_ReturnsConflictAndAsksNobody()
    {
        await PatchSettingsAsync("""{ "downloadLyrics": false }""");
        await _admin.WaitForJobsIdleAsync();
        int calls = _lrclib.ReceivedCalls().Count();

        await (await _admin.PostAsync($"/api/v1/tracks/{Track("Missing").Id}/lyrics/download", null))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "lyrics.download_disabled");
        _lrclib.ReceivedCalls().Should().HaveCount(calls);
    }

    [Test]
    public async Task SwitchingTheDownloadOn_DownloadsTheLyricsOfTracksWithoutLyricsAsFiles()
    {
        await SwitchOnAsync();

        LyricsDto lyrics = await _user.GetJsonAsync<LyricsDto>($"/api/v1/tracks/{Track("Plain").Id}/lyrics");

        lyrics.Source.Should().Be(LyricsSourceDto.File);
        lyrics.Type.Should().Be(LyricsTypeDto.LineSynchronized);
        lyrics.Lines.Select(line => line.Text).Should().Equal("Downloaded line");
        (await File.ReadAllTextAsync(_media.Combine("Band/Album/Plain.lrc")))
            .Should().Be("[00:00.10]Downloaded line");
        await _lrclib.DidNotReceive().GetAsync(
            Arg.Is<LrclibQuery>(query => query.TrackName == "Synced"),
            Arg.Any<CancellationToken>());
        await _lrclib.Received().GetAsync(
            Arg.Is<LrclibQuery>(query => query.TrackName == "Plain"
                && query.ArtistName == "Band"
                && query.AlbumName == "Album"
                && query.Duration > TimeSpan.Zero),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ScanAfterTheTagsLostTheirLyrics_DownloadsTheLyrics()
    {
        await SwitchOnAsync();
        string path = _media.Combine("Band/Album/Losing.mp3");
        await _lrclib.DidNotReceive().GetAsync(
            Arg.Is<LrclibQuery>(query => query.TrackName == "Losing"),
            Arg.Any<CancellationToken>());
        (await _user.GetJsonAsync<LyricsDto>($"/api/v1/tracks/{Track("Losing").Id}/lyrics"))
            .Source.Should().Be(LyricsSourceDto.Tags);

        using (var file = TagLib.File.Create(path))
        {
            file.Tag.Lyrics = null;
            file.Save();
        }

        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));
        HttpResponseMessage response = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        (await _admin.WaitForScanAsync(queued.Id)).Status.Should().Be(JobRunStatusDto.Succeeded);
        await _admin.WaitForJobsIdleAsync();

        LyricsDto lyrics = await _user.GetJsonAsync<LyricsDto>($"/api/v1/tracks/{Track("Losing").Id}/lyrics");
        lyrics.Source.Should().Be(LyricsSourceDto.File);
        lyrics.Lines.Select(line => line.Text).Should().Equal("Line from LRCLIB");
        (await File.ReadAllTextAsync(_media.Combine("Band/Album/Losing.lrc")))
            .Should().Be("[00:00.10]Line from LRCLIB");
    }

    [Test]
    public async Task DownloadTrackLyrics_TrackWithLyricsFile_KeepsTheFile()
    {
        await SwitchOnAsync();

        await (await _admin.PostAsync($"/api/v1/tracks/{Track("Synced").Id}/lyrics/download", null))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "lyrics.file_exists");
        (await File.ReadAllTextAsync(_media.Combine("Band/Album/Synced.lrc"))).Should().Be(EnhancedLrc);
    }

    [Test]
    public async Task DownloadTrackLyrics_NothingOrOnlyInstrumentalFound_ReturnsNotFound()
    {
        await SwitchOnAsync();

        await (await _admin.PostAsync($"/api/v1/tracks/{Track("Missing").Id}/lyrics/download", null))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "lyrics.download_not_found");
        await (await _admin.PostAsync($"/api/v1/tracks/{Track("Instrumental").Id}/lyrics/download", null))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "lyrics.instrumental");
        await (await _admin.PostAsync($"/api/v1/tracks/{Guid.NewGuid()}/lyrics/download", null))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "track.not_found");
    }

    [Test]
    public async Task DownloadTrackLyrics_ServiceUnreachableOrBusy_ReturnsServiceUnavailable()
    {
        await SwitchOnAsync();

        await (await _admin.PostAsync($"/api/v1/tracks/{Track("Tagged").Id}/lyrics/download", null))
            .ShouldBeProblemAsync(HttpStatusCode.ServiceUnavailable, "lyrics.service_unavailable");
        HttpResponseMessage busy = await _admin.PostAsync(
            $"/api/v1/tracks/{Track("Tagged Busy").Id}/lyrics/download",
            null);
        busy.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(30));
        await busy.ShouldBeProblemAsync(HttpStatusCode.ServiceUnavailable, "lyrics.rate_limited");
    }

    [TestCase("GET", "/api/v1/lyrics-lookups/summary")]
    [TestCase("DELETE", "/api/v1/lyrics-lookups")]
    [TestCase("GET", "/api/v1/tracks/{0}/lyrics/lookup")]
    [TestCase("DELETE", "/api/v1/tracks/{0}/lyrics/lookup")]
    public async Task LyricsLookups_WithoutTokenOrAsUser_AreRejected(string method, string path)
    {
        string url = string.Format(System.Globalization.CultureInfo.InvariantCulture, path, Track("Missing").Id);

        await (await _anonymous.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await (await _user.SendAsync(new HttpRequestMessage(new HttpMethod(method), url)))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Test]
    public async Task DeleteTrackLyricsLookup_LookupWithoutLyrics_ForgetsItAndKeepsResultsWithLyrics()
    {
        await SwitchOnAsync();
        string instrumental = $"/api/v1/tracks/{Track("Instrumental").Id}/lyrics";
        await (await _admin.PostAsync($"{instrumental}/download", null))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "lyrics.instrumental");

        LyricsLookupDto lookup = await _admin.GetJsonAsync<LyricsLookupDto>($"{instrumental}/lookup");
        lookup.Status.Should().Be(LyricsLookupStatusDto.Instrumental);
        lookup.IsForgettable.Should().BeTrue();
        (await _admin.DeleteAsync($"{instrumental}/lookup")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await (await _admin.GetAsync($"{instrumental}/lookup"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "lyrics.lookup_not_found");
        await (await _admin.DeleteAsync($"{instrumental}/lookup"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "lyrics.lookup_not_found");
        string plain = $"/api/v1/tracks/{Track("Plain").Id}/lyrics/lookup";
        (await _admin.GetJsonAsync<LyricsLookupDto>(plain)).Status.Should().Be(LyricsLookupStatusDto.SavedToFile);
        await (await _admin.DeleteAsync(plain))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "lyrics.lookup_found_lyrics");
        (await _admin.GetJsonAsync<LyricsLookupDto>(plain)).IsForgettable.Should().BeFalse();
        await (await _admin.GetAsync($"/api/v1/tracks/{Guid.NewGuid()}/lyrics/lookup"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "track.not_found");
        await (await _admin.DeleteAsync($"/api/v1/tracks/{Guid.NewGuid()}/lyrics/lookup"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "track.not_found");
    }

    [Test]
    public async Task DeleteLyricsLookups_LookupsWithoutLyrics_TheNextRunLooksTheTracksUpAgainAtOnce()
    {
        await SwitchOnAsync();
        Guid missing = Track("Missing").Id;
        await (await _admin.PostAsync($"/api/v1/tracks/{missing}/lyrics/download", null))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "lyrics.download_not_found");
        LyricsLookupSummaryDto before = await _admin.GetJsonAsync<LyricsLookupSummaryDto>(
            "/api/v1/lyrics-lookups/summary");
        before.NotFoundCount.Should().BeGreaterThanOrEqualTo(1);
        before.LookUpAgainAfterMinutes.Should().Be(30 * 24 * 60);

        // Within the wait the job does not ask LRCLIB again.
        int asked = LookupsOf("Missing");
        await RunLyricsDownloadAsync();
        LookupsOf("Missing").Should().Be(asked);

        (await _admin.DeleteAsync("/api/v1/lyrics-lookups")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        LyricsLookupSummaryDto after = await _admin.GetJsonAsync<LyricsLookupSummaryDto>(
            "/api/v1/lyrics-lookups/summary");
        after.NotFoundCount.Should().Be(0);
        after.InstrumentalCount.Should().Be(0);
        await (await _admin.GetAsync($"/api/v1/tracks/{missing}/lyrics/lookup"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "lyrics.lookup_not_found");
        (await _admin.GetJsonAsync<LyricsLookupDto>($"/api/v1/tracks/{Track("Plain").Id}/lyrics/lookup"))
            .Status.Should().Be(LyricsLookupStatusDto.SavedToFile);

        await RunLyricsDownloadAsync();

        LookupsOf("Missing").Should().BeGreaterThan(asked);
        (await _admin.GetJsonAsync<LyricsLookupDto>($"/api/v1/tracks/{missing}/lyrics/lookup"))
            .Status.Should().Be(LyricsLookupStatusDto.NotFound);
    }

    private int LookupsOf(string title)
    {
        return _lrclib.ReceivedCalls()
            .Count(call => call.GetArguments().OfType<LrclibQuery>().Any(query => query.TrackName == title));
    }

    private async Task RunLyricsDownloadAsync()
    {
        await _admin.WaitForJobsIdleAsync();
        HttpResponseMessage response = await _admin.PostAsync("/api/v1/jobs/lyrics-download/runs", null);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
        JobRunDto run = (await response.Content.ReadFromJsonAsync<JobRunDto>(HttpClientExtensions.JsonOptions))!;
        (await _admin.WaitForJobRunAsync("lyrics-download", run.Id)).Status.Should().Be(JobRunStatusDto.Succeeded);
    }

    private void Answer(string title, LrclibResponse response)
    {
        _lrclib.GetAsync(Arg.Is<LrclibQuery>(query => query.TrackName == title), Arg.Any<CancellationToken>())
            .Returns(response);
        _lrclib.SearchAsync(Arg.Is<LrclibQuery>(query => query.TrackName == title), Arg.Any<CancellationToken>())
            .Returns(response.Status == LrclibStatus.Found ? LrclibResponse.NotFound : response);
    }

    private async Task SwitchOnAsync()
    {
        await PatchSettingsAsync("""{ "downloadLyrics": true }""");
        await _admin.WaitForJobsIdleAsync();
    }

    private async Task PatchSettingsAsync(string body)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync("PATCH", "/api/v1/settings/library", body);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task AudioAsync(string title, string? lyrics = null)
    {
        string path = _media.Combine($"Band/Album/{title}.mp3");
        await TestMediaGenerator.CreateAudioAsync(
            path,
            AudioFormat.Mp3,
            new TestTags { Title = title, Artist = "Band", Album = "Album" });
        if (lyrics is not null)
        {
            using var file = TagLib.File.Create(path);
            file.Tag.Lyrics = lyrics;
            file.Save();
        }
    }

    private TrackDto Track(string title)
    {
        return _tracks.Single(track => track.Title == title);
    }
}
