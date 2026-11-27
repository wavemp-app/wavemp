using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// The review inbox through the API, with a library of synthetic media. The file watcher is off, so only the API
/// changes the library. The tests run one after another and put every track back into the inbox afterwards.
/// </summary>
[TestFixture]
[Category("Integration")]
[NonParallelizable]
public class TrackReviewsApiTests
{
    private TemporaryDirectory _media = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _user = null!;
    private ProfileDto _adminProfile = null!;
    private IReadOnlyList<TrackDto> _tracks = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        Directory.CreateDirectory(_media.Combine("Band/Album"));
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Band/Album/01 Complete.mp3"),
            AudioFormat.Mp3,
            new TestTags
            {
                Title = "Complete",
                Artist = "Band",
                Album = "Album",
                Track = "1",
                Date = "2001",
                Genre = "Rock",
            });
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Band/Album/02 Partial.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = "Partial", Artist = "Band", Album = "Album", Track = "2" });
        await TestMediaGenerator.CreateAudioAsync(_media.Combine("Loose - Untagged.mp3"), AudioFormat.Mp3);

        _factory = new WaveMpWebApplicationFactory(
            "wavemp_tests_service_track_reviews",
            freshDatabase: true,
            mediaPath: _media.Path);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        _adminProfile = await _anonymous.GetBuiltInAdminAsync();
        UserDto user = await _admin.CreateUserAsync(new { name = $"Listener {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        _user = _factory.CreateAuthorizedClient(session.Token);

        HttpResponseMessage settings = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/library",
            """{ "fileWatcherEnabled": false }""");
        settings.StatusCode.Should().Be(HttpStatusCode.OK, await settings.Content.ReadAsStringAsync());

        await ScanAsync();
        _tracks = (await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100")).Items;
        _tracks.Should().HaveCount(3);
    }

    [TearDown]
    public async Task TearDown()
    {
        await SetManyAsync(TrackReviewStatusDto.Unreviewed, [.. _tracks.Select(track => track.Id)]);
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

    [TestCase("GET", "/api/v1/track-reviews", null)]
    [TestCase("GET", "/api/v1/track-reviews/summary", null)]
    [TestCase("POST", "/api/v1/track-reviews/batch", """{ "trackIds": ["{id}"], "status": "Reviewed" }""")]
    [TestCase("GET", "/api/v1/tracks/{id}/review", null)]
    [TestCase("PUT", "/api/v1/tracks/{id}/review", """{ "status": "Reviewed" }""")]
    public async Task Endpoints_WithoutTokenOrAsUser_AreRejected(string method, string path, string? body)
    {
        string id = Track("Complete").Id.ToString();
        string target = path.Replace("{id}", id, StringComparison.Ordinal);
        string? content = body?.Replace("{id}", id, StringComparison.Ordinal);

        await (await _anonymous.SendJsonAsync(method, target, content))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await (await _user.SendJsonAsync(method, target, content))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
        (await SummaryAsync()).UnreviewedCount.Should().Be(3);
    }

    [Test]
    public async Task Summary_AfterTheScan_CountsEveryTrackAsUnreviewed()
    {
        TrackReviewSummaryDto summary = await SummaryAsync();

        summary.Should().BeEquivalentTo(new TrackReviewSummaryDto { UnreviewedCount = 3, ReviewedCount = 0 });
    }

    [Test]
    public async Task SetTrackReview_ReviewedAndUnreviewed_ChangesTheStatusAndTheCount()
    {
        Guid trackId = Track("Partial").Id;

        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PUT",
            $"/api/v1/tracks/{trackId}/review",
            """{ "status": "Reviewed" }""");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        TrackReviewStateDto reviewed =
            (await response.Content.ReadFromJsonAsync<TrackReviewStateDto>(HttpClientExtensions.JsonOptions))!;
        reviewed.Status.Should().Be(TrackReviewStatusDto.Reviewed);
        reviewed.TrackId.Should().Be(trackId);
        reviewed.ReviewedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        reviewed.ReviewedBy.Should().BeEquivalentTo(new ProfileReferenceDto
        {
            Id = _adminProfile.Id,
            Name = _adminProfile.Name,
        });
        (await _admin.GetJsonAsync<TrackReviewStateDto>($"/api/v1/tracks/{trackId}/review"))
            .Should().BeEquivalentTo(reviewed);
        (await SummaryAsync()).Should().BeEquivalentTo(
            new TrackReviewSummaryDto { UnreviewedCount = 2, ReviewedCount = 1 });
        (await ListAsync(string.Empty)).Select(item => item.Title).Should().NotContain("Partial");
        (await ListAsync("?status=Reviewed")).Should().ContainSingle().Which.ReviewedBy!.Id
            .Should().Be(_adminProfile.Id);

        HttpResponseMessage reset = await _admin.SendJsonAsync(
            "PUT",
            $"/api/v1/tracks/{trackId}/review",
            """{ "status": "Unreviewed" }""");

        reset.StatusCode.Should().Be(HttpStatusCode.OK);
        TrackReviewStateDto unreviewed =
            (await reset.Content.ReadFromJsonAsync<TrackReviewStateDto>(HttpClientExtensions.JsonOptions))!;
        unreviewed.Status.Should().Be(TrackReviewStatusDto.Unreviewed);
        unreviewed.ReviewedAt.Should().BeNull();
        unreviewed.ReviewedBy.Should().BeNull();
        (await SummaryAsync()).UnreviewedCount.Should().Be(3);
    }

    [Test]
    public async Task UpdateTrackReviews_SeveralTracks_MarksThemAtOnce()
    {
        Guid[] ids = [Track("Complete").Id, Track("Untagged").Id, Guid.NewGuid()];

        UpdatedTrackReviewsDto first = await SetManyAsync(TrackReviewStatusDto.Reviewed, ids);
        UpdatedTrackReviewsDto again = await SetManyAsync(TrackReviewStatusDto.Reviewed, ids);

        first.ChangedCount.Should().Be(2);
        again.ChangedCount.Should().Be(0);
        (await SummaryAsync()).Should().BeEquivalentTo(
            new TrackReviewSummaryDto { UnreviewedCount = 1, ReviewedCount = 2 });
        (await ListAsync(string.Empty)).Select(item => item.Title).Should().Equal("Partial");
    }

    [Test]
    public async Task Scan_ReviewedTrack_KeepsItsStatus()
    {
        Guid trackId = Track("Complete").Id;
        await SetManyAsync(TrackReviewStatusDto.Reviewed, [trackId]);

        await ScanAsync();

        (await _admin.GetJsonAsync<TrackReviewStateDto>($"/api/v1/tracks/{trackId}/review")).Status
            .Should().Be(TrackReviewStatusDto.Reviewed);
    }

    [Test]
    public async Task ListTrackReviews_Tracks_NameTheMissingTags()
    {
        IReadOnlyList<TrackReviewDto> items = await ListAsync("?sort=Title");

        items.Select(item => (item.Title, item.MissingTags)).Should().BeEquivalentTo(
            [
                ("Complete", (IReadOnlyList<MetadataTagDto>)[]),
                ("Partial", [MetadataTagDto.Year, MetadataTagDto.Genre]),
                ("Untagged", Enum.GetValues<MetadataTagDto>()),
            ],
            options => options.WithStrictOrdering());
        items[0].Should().BeEquivalentTo(new
        {
            Status = TrackReviewStatusDto.Unreviewed,
            DirectoryPath = "Band/Album",
            BaseName = "01 Complete",
            HasAudio = true,
            HasVideo = false,
            Artist = new { Name = "Band" },
            Album = new { Title = "Album" },
        });
        (await ListAsync("?missingTags=true&sort=Title&direction=Desc")).Select(item => item.Title)
            .Should().Equal("Untagged", "Partial");
        (await ListAsync("?q=loose")).Select(item => item.Title).Should().Equal("Untagged");
    }

    [Test]
    public async Task ListTrackReviews_InvalidQuery_ReturnsValidationProblem()
    {
        HttpResponseMessage response = await _admin.GetAsync("/api/v1/track-reviews?status=Unknown&sort=Unknown");

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Select(error => error.Field).Should().BeEquivalentTo("status", "sort");
    }

    [TestCase("GET", null)]
    [TestCase("PUT", """{ "status": "Reviewed" }""")]
    public async Task TrackReview_UnknownTrack_ReturnsNotFound(string method, string? body)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            method,
            $"/api/v1/tracks/{Guid.NewGuid()}/review",
            body);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "track.not_found");
    }

    [Test]
    public async Task SetTrackReview_WithoutStatus_ReturnsValidationProblem()
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PUT",
            $"/api/v1/tracks/{Track("Complete").Id}/review",
            "{}");

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "status",
            Code = ValidationErrorCodeDto.Required,
        });
    }

    [Test]
    public async Task UpdateTrackReviews_NoOrTooManyTracks_ReturnsValidationProblem()
    {
        string tooMany = string.Join(", ", Enumerable.Range(0, 501).Select(_ => $"\"{Guid.NewGuid()}\""));

        HttpResponseMessage empty = await _admin.SendJsonAsync(
            "POST",
            "/api/v1/track-reviews/batch",
            """{ "trackIds": [], "status": "Reviewed" }""");
        HttpResponseMessage many = await _admin.SendJsonAsync(
            "POST",
            "/api/v1/track-reviews/batch",
            $$"""{ "trackIds": [{{tooMany}}], "status": "Reviewed" }""");

        (await empty.ShouldBeValidationProblemAsync()).Should().ContainSingle().Which.Field.Should().Be("trackIds");
        (await many.ShouldBeValidationProblemAsync()).Should().ContainSingle().Which.Field.Should().Be("trackIds");
        (await SummaryAsync()).UnreviewedCount.Should().Be(3);
    }

    private TrackDto Track(string title)
    {
        return _tracks.Single(track => track.Title == title);
    }

    private Task<TrackReviewSummaryDto> SummaryAsync()
    {
        return _admin.GetJsonAsync<TrackReviewSummaryDto>("/api/v1/track-reviews/summary");
    }

    private async Task<IReadOnlyList<TrackReviewDto>> ListAsync(string query)
    {
        return (await _admin.GetJsonAsync<PageDto<TrackReviewDto>>($"/api/v1/track-reviews{query}")).Items;
    }

    private async Task<UpdatedTrackReviewsDto> SetManyAsync(TrackReviewStatusDto status, IReadOnlyList<Guid> ids)
    {
        HttpResponseMessage response = await _admin.PostAsJsonAsync(
            "/api/v1/track-reviews/batch",
            new { trackIds = ids, status = status.ToString() },
            HttpClientExtensions.JsonOptions);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<UpdatedTrackReviewsDto>(HttpClientExtensions.JsonOptions))!;
    }

    private async Task ScanAsync()
    {
        HttpResponseMessage response = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        ScanDto scan = await _admin.WaitForScanAsync(queued.Id);
        scan.Status.Should().Be(JobRunStatusDto.Succeeded);
    }
}
