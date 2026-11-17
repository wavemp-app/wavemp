using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Selects the files of tracks of a library of synthetic media through the API. The file watcher is off, so only the
/// API changes the library.
/// </summary>
[TestFixture]
[Category("Integration")]
public class FileSelectionApiTests
{
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
        Directory.CreateDirectory(_media.Combine("Band/Album"));
        await AudioAsync("Band/Album/Song.flac", AudioFormat.Flac, "Song Lossless");
        await AudioAsync("Band/Album/Song.mp3", AudioFormat.Mp3, "Song Lossy");
        await TestMediaGenerator.CreateVideoAsync(_media.Combine("Band/Album/Song.mkv"), VideoFormat.Mkv, 640, 480);
        await TestMediaGenerator.CreateVideoAsync(_media.Combine("Band/Album/Song.mp4"), VideoFormat.Mp4, 320, 240);
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Band/Album/Song.jpg"), 100, 100);
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Band/Album/Song.png"), 300, 300);
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Band/Album/cover.jpg"), 200, 200);
        await AudioAsync("Band/Album/Plain.mp3", AudioFormat.Mp3, "Plain");

        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_file_selection",
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

    [TestCase("GET")]
    [TestCase("PUT")]
    [TestCase("DELETE")]
    public async Task TrackFileSelection_WithoutTokenOrAsUser_IsRejected(string method)
    {
        string path = $"/api/v1/tracks/{Track("Plain").Id}/file-selection";
        string? body = method == "PUT" ? "{}" : null;

        await (await _anonymous.SendJsonAsync(method, path, body))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await (await _user.SendJsonAsync(method, path, body))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Test]
    public async Task ListFileSelections_AsUser_ReturnsForbidden()
    {
        HttpResponseMessage response = await _user.GetAsync("/api/v1/file-selections");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [TestCase("GET")]
    [TestCase("PUT")]
    [TestCase("DELETE")]
    public async Task TrackFileSelection_UnknownTrack_ReturnsNotFound(string method)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            method,
            $"/api/v1/tracks/{Guid.NewGuid()}/file-selection",
            method == "PUT" ? "{}" : null);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "track.not_found");
    }

    [Test]
    public async Task SetTrackFileSelection_FilesOfOtherCategoriesOrTracks_ReturnsTheirFields()
    {
        Dictionary<string, Guid> song = await FileIdsAsync("Song Lossless", "Song Lossy");
        Dictionary<string, Guid> plain = await FileIdsAsync("Plain");

        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PUT",
            $"/api/v1/tracks/{Track("Plain").Id}/file-selection",
            $$"""{ "audioFileId": "{{song["Song.flac"]}}", "videoFileId": "{{plain["Plain.mp3"]}}" }""");

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Select(error => (error.Field, error.Code)).Should().BeEquivalentTo(
        [
            ("audioFileId", ValidationErrorCodeDto.InvalidValue),
            ("videoFileId", ValidationErrorCodeDto.InvalidValue),
        ]);
    }

    [Test]
    public async Task TrackFileSelection_SelectAndReset_ChangesWhatTheTrackPlaysAndShows()
    {
        Guid trackId = SongId();
        Dictionary<string, Guid> files = await FileIdsAsync("Song Lossless", "Song Lossy");
        string albumCover = await CoverFileOfAsync(trackId);

        TrackFileSelectionDto automatic = await _admin.GetJsonAsync<TrackFileSelectionDto>(
            $"/api/v1/tracks/{trackId}/file-selection");
        automatic.Audio.State.Should().Be(FileSelectionStateDto.Automatic);
        automatic.Audio.ActiveFileId.Should().Be(files["Song.flac"]);
        automatic.Audio.CandidateFileIds.Should().Equal(files["Song.flac"], files["Song.mp3"]);
        automatic.Video.AutomaticFileId.Should().Be(files["Song.mkv"]);
        automatic.Cover.CandidateFileIds.Should().Equal(files["Song.png"], files["Song.jpg"]);

        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PUT",
            $"/api/v1/tracks/{trackId}/file-selection",
            $$"""
            {
              "audioFileId": "{{files["Song.mp3"]}}",
              "videoFileId": "{{files["Song.mp4"]}}",
              "coverFileId": "{{files["Song.jpg"]}}"
            }
            """);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        TrackFileSelectionDto selected =
            (await response.Content.ReadFromJsonAsync<TrackFileSelectionDto>(HttpClientExtensions.JsonOptions))!;
        selected.Audio.Should().BeEquivalentTo(new
        {
            State = FileSelectionStateDto.Selected,
            SelectedFileId = files["Song.mp3"],
            SelectedPath = "Band/Album/Song.mp3",
            ActiveFileId = files["Song.mp3"],
            AutomaticFileId = files["Song.flac"],
        });
        selected.Video.ActiveFileId.Should().Be(files["Song.mp4"]);
        selected.Cover.ActiveFileId.Should().Be(files["Song.jpg"]);

        PageDto<MediaFileDto> primary = await _admin.GetJsonAsync<PageDto<MediaFileDto>>(
            $"/api/v1/tracks/{trackId}/files");
        primary.Items.Where(file => file.IsPrimary).Select(file => file.FileName)
            .Should().BeEquivalentTo("Song.mp3", "Song.mp4");
        (await _admin.GetJsonAsync<TrackDto>($"/api/v1/tracks/{trackId}")).Title.Should().Be("Song Lossy");
        (await CoverFileOfAsync(trackId)).Should().Be(files["Song.jpg"].ToString("N"));
        PageDto<TrackFileSummaryDto> list = await _admin.GetJsonAsync<PageDto<TrackFileSummaryDto>>(
            "/api/v1/file-selections?filter=Selected");
        list.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            TrackId = trackId,
            BaseName = "Song",
            DirectoryPath = "Band/Album",
            AudioCount = 2,
            VideoCount = 2,
            CoverCount = 2,
            Audio = FileSelectionStateDto.Selected,
            Video = FileSelectionStateDto.Selected,
            Cover = FileSelectionStateDto.Selected,
        });

        HttpResponseMessage reset = await _admin.DeleteAsync($"/api/v1/tracks/{trackId}/file-selection");

        reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        TrackFileSelectionDto after = await _admin.GetJsonAsync<TrackFileSelectionDto>(
            $"/api/v1/tracks/{trackId}/file-selection");
        after.Audio.State.Should().Be(FileSelectionStateDto.Automatic);
        after.Audio.ActiveFileId.Should().Be(files["Song.flac"]);
        (await _admin.GetJsonAsync<TrackDto>($"/api/v1/tracks/{trackId}")).Title.Should().Be("Song Lossless");
        (await CoverFileOfAsync(trackId)).Should().Be(albumCover);
        (await _admin.GetJsonAsync<PageDto<TrackFileSummaryDto>>("/api/v1/file-selections?filter=Selected")).Total
            .Should().Be(0);
    }

    [Test]
    public async Task ListFileSelections_InvalidFilter_ReturnsValidationProblem()
    {
        HttpResponseMessage response = await _admin.GetAsync("/api/v1/file-selections?filter=Unknown");

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Field.Should().Be("filter");
    }

    [Test]
    public async Task ListFileSelections_SeveralAudio_ListsOnlyTracksWithAChoice()
    {
        PageDto<TrackFileSummaryDto> page = await _admin.GetJsonAsync<PageDto<TrackFileSummaryDto>>(
            "/api/v1/file-selections?filter=SeveralAudio");

        page.Items.Select(item => item.BaseName).Should().Equal("Song");
    }

    private TrackDto Track(string title)
    {
        return _tracks.Single(track => track.Title == title);
    }

    /// <summary>
    /// The song is found by either title: a test may have selected the MP3 for a moment.
    /// </summary>
    private Guid SongId()
    {
        return _tracks.Single(track => track.Title.StartsWith("Song", StringComparison.Ordinal)).Id;
    }

    private async Task<Dictionary<string, Guid>> FileIdsAsync(params string[] titles)
    {
        Guid trackId = _tracks.Single(track => titles.Contains(track.Title)).Id;
        PageDto<MediaFileDto> files = await _admin.GetJsonAsync<PageDto<MediaFileDto>>(
            $"/api/v1/tracks/{trackId}/files");
        return files.Items.ToDictionary(file => file.FileName, file => file.Id);
    }

    /// <summary>
    /// The id (32 hex digits) of the file the cover of the track comes from, read from its ETag.
    /// </summary>
    private async Task<string> CoverFileOfAsync(Guid trackId)
    {
        HttpResponseMessage response = await _admin.GetAsync($"/api/v1/tracks/{trackId}/cover");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return response.Headers.ETag!.Tag.Trim('"')[..32];
    }

    private Task AudioAsync(string relativePath, AudioFormat format, string title)
    {
        return TestMediaGenerator.CreateAudioAsync(
            _media.Combine(relativePath),
            format,
            new TestTags { Title = title, Artist = "Band", Album = "Album" });
    }
}
