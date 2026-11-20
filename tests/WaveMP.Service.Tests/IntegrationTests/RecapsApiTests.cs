using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Statistics, shared recaps and the conversion of recap videos through the API, with a small library of synthetic
/// media and plays of the last hours.
/// </summary>
[TestFixture]
[Category("Integration")]
public class RecapsApiTests
{
    private const string StatisticsPath = "/api/v1/me/stats";
    private const string SharesPath = "/api/v1/recap-shares";
    private const string VideosPath = "/api/v1/me/recap-videos";

    private TemporaryDirectory _media = null!;
    private TemporaryDirectory _videos = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private IReadOnlyList<TrackDto> _tracks = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        _videos = new TemporaryDirectory();
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Aurora/Daybreak/First.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = "First", Artist = "Aurora", Album = "Daybreak", Genre = "Pop" });
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Aurora/Daybreak/Second.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = "Second", Artist = "Aurora", Album = "Daybreak", Genre = "pop" });
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Breeze/Evening/Third.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = "Third", Artist = "Breeze", Album = "Evening", Genre = "Rock" });

        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_recaps",
            freshDatabase: true,
            mediaPath: _media.Path);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();

        HttpResponseMessage response = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        ScanDto scan = await _admin.WaitForScanAsync(queued.Id);
        scan.Status.Should().Be(JobRunStatusDto.Succeeded);

        _tracks = (await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100")).Items;
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
        _media.Dispose();
        _videos.Dispose();
    }

    [Test]
    public async Task Recaps_WithoutToken_ReturnUnauthorized()
    {
        HttpResponseMessage statistics = await _anonymous.GetAsync(StatisticsPath);
        HttpResponseMessage shares = await _anonymous.GetAsync(SharesPath);
        HttpResponseMessage video = await _anonymous.PostAsync(VideosPath, new ByteArrayContent([1]));

        await statistics.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await shares.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await video.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task GetMyStatistics_WithPlays_CountsTheRankingsAndTotalsOfTheProfile()
    {
        using HttpClient client = await CreateProfileAsync();
        using HttpClient other = await CreateProfileAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await PlayAsync(client, "Third", now.AddMinutes(-50));
        await PlayAsync(client, "Third", now.AddMinutes(-40));
        await PlayAsync(client, "First", now.AddMinutes(-30));
        await PlayAsync(client, "Second", now.AddMinutes(-20));
        await PlayAsync(other, "First", now.AddMinutes(-10));

        ListeningStatisticsDto statistics = await client.GetJsonAsync<ListeningStatisticsDto>(
            $"{StatisticsPath}?from={Day(now.AddDays(-1))}&to={Day(now)}&timeZone=UTC");

        statistics.From.Should().Be(DateOnly.FromDateTime(now.AddDays(-1).UtcDateTime));
        statistics.To.Should().Be(DateOnly.FromDateTime(now.UtcDateTime));
        statistics.TimeZone.Should().Be("UTC");
        statistics.Totals.Plays.Should().Be(4);
        statistics.Totals.Tracks.Should().Be(3);
        statistics.Totals.Artists.Should().Be(2);
        statistics.Totals.Albums.Should().Be(2);
        statistics.Totals.Genres.Should().Be(2);
        statistics.Totals.NewArtists.Should().Be(2);
        statistics.Totals.ListeningMilliseconds.Should().BePositive();
        statistics.TopTracks.Select(track => (track.Track.Title, track.Plays)).Should().StartWith([("Third", 2)]);
        statistics.TopArtists.Select(artist => (artist.Artist.Name, artist.Plays)).Should().Equal(
            ("Aurora", 2),
            ("Breeze", 2));
        statistics.TopGenres.Select(genre => genre.Plays).Should().Equal(2, 2);
        statistics.TopGenres.Select(genre => genre.Name.ToUpperInvariant()).Should().BeEquivalentTo("POP", "ROCK");
        statistics.Timeline.Granularity.Should().Be(TimelineGranularityDto.Day);
        statistics.Timeline.Buckets.Should().HaveCount(2);
        statistics.Timeline.Buckets.Sum(bucket => bucket.Plays).Should().Be(4);
        statistics.Hours.Should().HaveCount(24);
        statistics.Hours.Sum(hour => hour.Plays).Should().Be(4);
    }

    [Test]
    public async Task GetMyStatistics_PeriodWithoutPlays_ReturnsEmptyRankings()
    {
        using HttpClient client = await CreateProfileAsync();
        await PlayAsync(client, "First", DateTimeOffset.UtcNow.AddMinutes(-5));

        ListeningStatisticsDto statistics = await client.GetJsonAsync<ListeningStatisticsDto>(
            $"{StatisticsPath}?from=2020-01-01&to=2020-01-31&timeZone=Europe/Berlin&top=3");

        statistics.Totals.Plays.Should().Be(0);
        statistics.TopTracks.Should().BeEmpty();
        statistics.Timeline.Buckets.Should().HaveCount(31);
    }

    [Test]
    public async Task GetMyStatistics_WithoutDays_CountsFromTheFirstPlay()
    {
        using HttpClient client = await CreateProfileAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await PlayAsync(client, "First", now.AddDays(-3));
        await PlayAsync(client, "Second", now.AddMinutes(-5));

        ListeningStatisticsDto statistics = await client.GetJsonAsync<ListeningStatisticsDto>(StatisticsPath);

        statistics.From.Should().Be(DateOnly.FromDateTime(now.AddDays(-3).UtcDateTime));
        statistics.Totals.Plays.Should().Be(2);
    }

    [TestCase("from=2026-02-01&to=2026-01-01", "from")]
    [TestCase("timeZone=Mars/Olympus_Mons", "timeZone")]
    [TestCase("from=1800-01-01", "from")]
    public async Task GetMyStatistics_InvalidQuery_ReturnsInvalidValue(string query, string field)
    {
        using HttpClient client = await CreateProfileAsync();

        HttpResponseMessage response = await client.GetAsync($"{StatisticsPath}?{query}");

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Field = field, Code = ValidationErrorCodeDto.InvalidValue });
    }

    [TestCase("from=yesterday", "from", ValidationErrorCodeDto.InvalidFormat)]
    [TestCase("top=0", "top", ValidationErrorCodeDto.OutOfRange)]
    public async Task GetMyStatistics_MalformedQuery_ReturnsTheCode(
        string query,
        string field,
        ValidationErrorCodeDto code)
    {
        using HttpClient client = await CreateProfileAsync();

        HttpResponseMessage response = await client.GetAsync($"{StatisticsPath}?{query}");

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Field = field, Code = code });
    }

    [Test]
    public async Task RecapShares_SharedRecap_IsSeenByOtherProfilesWithTheStatisticsOfItsOwner()
    {
        using HttpClient owner = await CreateProfileAsync();
        using HttpClient viewer = await CreateProfileAsync();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await PlayAsync(owner, "Third", now.AddMinutes(-10));

        HttpResponseMessage created = await owner.PostAsJsonAsync(
            SharesPath,
            new
            {
                period = "LastMonth",
                from = Day(now.AddMonths(-1)),
                to = Day(now),
                timeZone = "UTC",
                design = "aurora",
            });

        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        RecapShareDto share =
            (await created.Content.ReadFromJsonAsync<RecapShareDto>(HttpClientExtensions.JsonOptions))!;
        created.Headers.Location!.AbsolutePath.Should().Be($"{SharesPath}/{share.Id}");
        share.IsOwn.Should().BeTrue();
        share.Period.Should().Be(RecapPeriodDto.LastMonth);
        share.Design.Should().Be("aurora");

        PageDto<RecapShareDto> received = await viewer.GetJsonAsync<PageDto<RecapShareDto>>(SharesPath);
        RecapShareDto seen = received.Items.Should().ContainSingle(item => item.Id == share.Id).Subject;
        seen.IsOwn.Should().BeFalse();
        seen.Owner.Id.Should().Be(share.Owner.Id);
        (await owner.GetJsonAsync<PageDto<RecapShareDto>>(SharesPath)).Items.Should().NotContain(
            item => item.Id == share.Id);
        (await owner.GetJsonAsync<PageDto<RecapShareDto>>($"{SharesPath}?scope=Own")).Items
            .Should().ContainSingle(item => item.Id == share.Id);

        SharedRecapDto recap = await viewer.GetJsonAsync<SharedRecapDto>($"{SharesPath}/{share.Id}/statistics");
        recap.Share.Id.Should().Be(share.Id);
        recap.Statistics.Totals.Plays.Should().Be(1);
        recap.Statistics.TopTracks.Should().ContainSingle().Which.Track.Title.Should().Be("Third");
    }

    [Test]
    public async Task CreateRecapShare_SamePeriodAgain_ChangesTheDesignOfTheShare()
    {
        using HttpClient owner = await CreateProfileAsync();
        object request = new { period = "AllTime", to = "2026-11-20", timeZone = "Europe/Berlin", design = "aurora" };
        HttpResponseMessage first = await owner.PostAsJsonAsync(SharesPath, request);
        RecapShareDto created =
            (await first.Content.ReadFromJsonAsync<RecapShareDto>(HttpClientExtensions.JsonOptions))!;

        HttpResponseMessage again = await owner.PostAsJsonAsync(
            SharesPath,
            new { period = "AllTime", to = "2026-11-20", timeZone = "Europe/Berlin", design = "prism" });

        again.StatusCode.Should().Be(HttpStatusCode.OK, await again.Content.ReadAsStringAsync());
        RecapShareDto updated =
            (await again.Content.ReadFromJsonAsync<RecapShareDto>(HttpClientExtensions.JsonOptions))!;
        updated.Id.Should().Be(created.Id);
        updated.Design.Should().Be("prism");
        updated.From.Should().BeNull();
    }

    [Test]
    public async Task CreateRecapShare_InvalidRequest_ReturnsTheFieldErrors()
    {
        using HttpClient owner = await CreateProfileAsync();

        HttpResponseMessage response = await owner.PostAsJsonAsync(
            SharesPath,
            new
            {
                period = "Custom",
                from = "2026-02-01",
                to = "2026-01-01",
                timeZone = "Nowhere",
                design = "Big Design",
            });

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Select(error => (error.Field, error.Code)).Should().BeEquivalentTo(
        [
            ("design", ValidationErrorCodeDto.InvalidFormat),
        ]);
    }

    [Test]
    public async Task CreateRecapShare_InvalidPeriod_ReturnsInvalidValues()
    {
        using HttpClient owner = await CreateProfileAsync();

        HttpResponseMessage response = await owner.PostAsJsonAsync(
            SharesPath,
            new { period = "Custom", from = "2026-02-01", to = "2026-01-01", timeZone = "Nowhere", design = "frost" });

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Select(error => (error.Field, error.Code)).Should().BeEquivalentTo(
        [
            ("from", ValidationErrorCodeDto.InvalidValue),
            ("timeZone", ValidationErrorCodeDto.InvalidValue),
        ]);
    }

    [Test]
    public async Task DeleteRecapShare_ByAnotherProfile_IsForbiddenAndByTheOwnerRemovesTheShare()
    {
        using HttpClient owner = await CreateProfileAsync();
        using HttpClient other = await CreateProfileAsync();
        HttpResponseMessage created = await owner.PostAsJsonAsync(
            SharesPath,
            new { period = "LastYear", from = "2025-11-21", to = "2026-11-20", timeZone = "UTC", design = "frost" });
        RecapShareDto share =
            (await created.Content.ReadFromJsonAsync<RecapShareDto>(HttpClientExtensions.JsonOptions))!;

        HttpResponseMessage forbidden = await other.DeleteAsync($"{SharesPath}/{share.Id}");
        HttpResponseMessage deleted = await owner.DeleteAsync($"{SharesPath}/{share.Id}");
        HttpResponseMessage gone = await other.GetAsync($"{SharesPath}/{share.Id}");
        HttpResponseMessage goneStatistics = await other.GetAsync($"{SharesPath}/{share.Id}/statistics");

        await forbidden.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await gone.ShouldBeProblemAsync(HttpStatusCode.NotFound, "recap.share_not_found");
        await goneStatistics.ShouldBeProblemAsync(HttpStatusCode.NotFound, "recap.share_not_found");
    }

    [Test]
    [Category("Media")]
    public async Task ConvertMyRecapVideo_WebmRecording_ReturnsAnMp4WithH264AndAac()
    {
        using HttpClient client = await CreateProfileAsync();
        string webm = _videos.Combine("recording.webm");
        await TestMediaGenerator.CreateVideoAsync(webm, VideoFormat.Webm, 320, 568, codec: TestVideoCodec.Vp9);
        using ByteArrayContent content = new(await File.ReadAllBytesAsync(webm));
        content.Headers.ContentType = new MediaTypeHeaderValue("video/webm");

        HttpResponseMessage response = await client.PostAsync(VideosPath, content);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.Should().Be("video/mp4");
        string mp4 = _videos.Combine("converted.mp4");
        await File.WriteAllBytesAsync(mp4, await response.Content.ReadAsByteArrayAsync());
        string streams = await FfprobeStreamsAsync(mp4);
        streams.Should().Contain("h264").And.Contain("aac");
    }

    [Test]
    public async Task ConvertMyRecapVideo_NoVideo_ReturnsUnsupportedFormat()
    {
        using HttpClient client = await CreateProfileAsync();
        using ByteArrayContent content = new("this is not a video at all"u8.ToArray());
        content.Headers.ContentType = new MediaTypeHeaderValue("video/webm");

        HttpResponseMessage response = await client.PostAsync(VideosPath, content);

        await response.ShouldBeProblemAsync(HttpStatusCode.UnsupportedMediaType, "video.unsupported_format");
    }

    private Guid Track(string title)
    {
        return _tracks.Single(track => track.Title == title).Id;
    }

    private async Task<HttpClient> CreateProfileAsync()
    {
        UserDto user = await _admin.CreateUserAsync(new { name = $"Recap {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        return _factory.CreateAuthorizedClient(session.Token);
    }

    private async Task PlayAsync(HttpClient client, string title, DateTimeOffset playedAt)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/me/history",
            new { trackId = Track(title), playedAt },
            HttpClientExtensions.JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private static string Day(DateTimeOffset moment)
    {
        return DateOnly.FromDateTime(moment.UtcDateTime)
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static async Task<string> FfprobeStreamsAsync(string path)
    {
        using Process process = new()
        {
            StartInfo = new ProcessStartInfo(FfmpegTool.FfprobePath)
            {
                ArgumentList =
                {
                    "-v", "error", "-show_entries", "stream=codec_name", "-of", "csv=p=0", path,
                },
                RedirectStandardOutput = true,
                UseShellExecute = false,
            },
        };
        process.Start();
        string output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();
        return output;
    }
}
