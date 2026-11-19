using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// The library check through the API, with a library of synthetic media: the scan starts the check, and the
/// administration lists the problems it found by type. The file watcher is off, so only the API changes the library.
/// </summary>
[TestFixture]
[Category("Integration")]
[NonParallelizable]
public class LibraryIssuesApiTests
{
    private TemporaryDirectory _media = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _user = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Band/Album/01 Twin.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = "Twin", Artist = "Band", Album = "Album" });
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Band/Album/cover.jpg"), 100, 100);
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Band/Live/Twin.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = "Twin", Artist = "Band", Album = "Live" });
        await TestMediaGenerator.CreateAudioAsync(_media.Combine("Loose - Untagged.mp3"), AudioFormat.Mp3);
        TestMediaGenerator.CreateCorruptFile(_media.Combine("Broken/Broken.mp3"));
        await TestMediaGenerator.CreateVideoAsync(_media.Combine("Clips/Clip.mkv"), VideoFormat.Mkv, 160, 120);

        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_library_issues",
            freshDatabase: true,
            mediaPath: _media.Path);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        UserDto user = await _admin.CreateUserAsync(new { name = $"Listener {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        _user = _factory.CreateAuthorizedClient(session.Token);

        HttpResponseMessage settings = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/library",
            """{ "fileWatcherEnabled": false }""");
        settings.StatusCode.Should().Be(HttpStatusCode.OK, await settings.Content.ReadAsStringAsync());

        // The scan starts the check; WaitForScanAsync waits for the jobs it started.
        HttpResponseMessage response = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        (await _admin.WaitForScanAsync(queued.Id)).Status.Should().Be(JobRunStatusDto.Succeeded);
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

    [TestCase("/api/v1/library/issues")]
    [TestCase("/api/v1/library/issues/summary")]
    public async Task Endpoints_WithoutTokenOrAsUser_AreRejected(string path)
    {
        await (await _anonymous.GetAsync(path)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await (await _user.GetAsync(path)).ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Test]
    public async Task GetLibraryIssueSummary_AfterTheScan_CountsTheProblemsOfEveryType()
    {
        LibraryIssueSummaryDto summary = await _admin.GetJsonAsync<LibraryIssueSummaryDto>(
            "/api/v1/library/issues/summary");

        summary.CheckedAt.Should().NotBeNull();
        summary.TrackCount.Should().Be(5);
        summary.Counts.Select(count => count.Type).Should().Equal(Enum.GetValues<LibraryIssueTypeDto>());
        summary.Counts.Should().AllSatisfy(count => count.Count.Should().BePositive(count.Type.ToString()));
        summary.ProblemCount.Should().Be(summary.Counts
            .Where(count => count.Severity == LibraryIssueSeverityDto.Problem)
            .Sum(count => count.Count));
        summary.HintCount.Should().Be(summary.Counts
            .Where(count => count.Severity == LibraryIssueSeverityDto.Hint)
            .Sum(count => count.Count));
    }

    [Test]
    public async Task ListLibraryIssues_ByType_ListsTheProblemsOfTheTypesWithTheirDetails()
    {
        PageDto<LibraryIssueDto> page = await _admin.GetJsonAsync<PageDto<LibraryIssueDto>>(
            "/api/v1/library/issues?type=PossibleDuplicate&type=UnreadableFile&limit=100");

        page.Items.Select(issue => (issue.Type, issue.Track!.DirectoryPath)).Should().BeEquivalentTo(
        [
            (LibraryIssueTypeDto.PossibleDuplicate, "Band/Album"),
            (LibraryIssueTypeDto.PossibleDuplicate, "Band/Live"),
            (LibraryIssueTypeDto.UnreadableFile, "Broken"),
        ]);
        LibraryIssueDto duplicate = page.Items.First(issue => issue.Type == LibraryIssueTypeDto.PossibleDuplicate);
        duplicate.RelatedTrack!.Title.Should().Be("Twin");
        duplicate.RelatedTrack.Id.Should().NotBe(duplicate.Track!.Id);
        LibraryIssueDto unreadable = page.Items.Single(issue => issue.Type == LibraryIssueTypeDto.UnreadableFile);
        unreadable.File!.RelativePath.Should().Be("Broken/Broken.mp3");
        unreadable.FileIssue.Should().NotBeNull();
        unreadable.Severity.Should().Be(LibraryIssueSeverityDto.Problem);
    }

    [Test]
    public async Task ListLibraryIssues_HintsAndSearch_FilterTheProblems()
    {
        PageDto<LibraryIssueDto> hints = await _admin.GetJsonAsync<PageDto<LibraryIssueDto>>(
            "/api/v1/library/issues?severity=Hint&limit=100");
        PageDto<LibraryIssueDto> untagged = await _admin.GetJsonAsync<PageDto<LibraryIssueDto>>(
            "/api/v1/library/issues?q=untagged&sort=Title&limit=100");

        hints.Items.Should().NotBeEmpty()
            .And.AllSatisfy(issue => issue.Severity.Should().Be(LibraryIssueSeverityDto.Hint));
        hints.Items.Should().ContainSingle(issue => issue.Type == LibraryIssueTypeDto.VideoWithoutAudio)
            .Which.Track!.BaseName.Should().Be("Clip");
        untagged.Items.Should().OnlyContain(issue => issue.Track!.BaseName == "Loose - Untagged");
        untagged.Items.Select(issue => issue.Type).Should().Contain(
        [
            LibraryIssueTypeDto.MissingTags,
            LibraryIssueTypeDto.UnknownAlbum,
            LibraryIssueTypeDto.MissingCover,
        ]);
        untagged.Items.Single(issue => issue.Type == LibraryIssueTypeDto.MissingTags).MissingTags
            .Should().Equal(MetadataTagDto.Title, MetadataTagDto.Artist, MetadataTagDto.Album);
    }

    [TestCase("type=Unknown")]
    [TestCase("type=99")]
    [TestCase("severity=Severe")]
    [TestCase("sort=Size")]
    [TestCase("limit=0")]
    public async Task ListLibraryIssues_InvalidQuery_IsAValidationProblem(string query)
    {
        HttpResponseMessage response = await _admin.GetAsync($"/api/v1/library/issues?{query}");

        await response.ShouldBeValidationProblemAsync();
    }

    [Test]
    public async Task StartJobRun_LibraryCheck_ChecksTheLibraryAgain()
    {
        LibraryIssueSummaryDto before = await _admin.GetJsonAsync<LibraryIssueSummaryDto>(
            "/api/v1/library/issues/summary");

        HttpResponseMessage response = await _admin.PostAsync("/api/v1/jobs/library-check/runs", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        JobRunDto run = (await response.Content.ReadFromJsonAsync<JobRunDto>(HttpClientExtensions.JsonOptions))!;
        (await _admin.WaitForJobRunAsync("library-check", run.Id)).Status.Should().Be(JobRunStatusDto.Succeeded);
        LibraryIssueSummaryDto after = await _admin.GetJsonAsync<LibraryIssueSummaryDto>(
            "/api/v1/library/issues/summary");
        after.CheckedAt.Should().BeAfter(before.CheckedAt!.Value);
        after.Counts.Should().BeEquivalentTo(before.Counts);
    }
}
