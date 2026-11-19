using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Manages media sources through the API: the first source is the media folder of the service, further sources are
/// temporary folders with synthetic media.
/// </summary>
[TestFixture]
[Category("Integration")]
public class MediaSourcesApiTests
{
    private TemporaryDirectory _media = null!;
    private TemporaryDirectory _others = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _user = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        _others = new TemporaryDirectory();
        await TestMediaGenerator.CreateAudioAsync(_media.Combine("Band/Song.mp3"), AudioFormat.Mp3);

        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_sources",
            freshDatabase: true,
            mediaPath: _media.Path);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        UserDto user = await _admin.CreateUserAsync(new { name = $"User {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        _user = _factory.CreateAuthorizedClient(session.Token);

        HttpResponseMessage scan = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await scan.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        await _admin.WaitForScanAsync(queued.Id);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _user?.Dispose();
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
        _others.Dispose();
        _media.Dispose();
    }

    private static IEnumerable<TestCaseData> AdminEndpoints()
    {
        string id = Guid.NewGuid().ToString();
        yield return new TestCaseData("POST", "/api/v1/sources").SetArgDisplayNames("CreateMediaSource");
        yield return new TestCaseData("PATCH", $"/api/v1/sources/{id}").SetArgDisplayNames("UpdateMediaSource");
        yield return new TestCaseData("DELETE", $"/api/v1/sources/{id}").SetArgDisplayNames("DeleteMediaSource");
    }

    [TestCase("GET", "/api/v1/sources")]
    [TestCase("POST", "/api/v1/sources")]
    [TestCase("DELETE", "/api/v1/sources/019a0000-0000-7000-8000-000000000001")]
    public async Task SourceEndpoints_WithoutToken_ReturnUnauthorized(string method, string path)
    {
        HttpResponseMessage response = await _anonymous.SendJsonAsync(method, path);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [TestCaseSource(nameof(AdminEndpoints))]
    public async Task ChangingEndpoints_AsUser_ReturnForbidden(string method, string path)
    {
        HttpResponseMessage response = await _user.SendJsonAsync(method, path, "{}");

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Test]
    public async Task ListMediaSources_AsUser_ContainsTheMediaFolderAsTheFirstSource()
    {
        PageDto<MediaSourceDto> page = await _user.GetJsonAsync<PageDto<MediaSourceDto>>("/api/v1/sources");

        MediaSourceDto first = page.Items.Single(source => source.Id == MediaSource.DefaultId);
        first.Path.Should().Be(_media.Path);
        first.FollowsMediaPath.Should().BeTrue();
        first.IsActive.Should().BeTrue();
        first.IsAvailable.Should().BeTrue();
        first.TrackCount.Should().Be(1);
        first.FileCount.Should().Be(1);
        page.Total.Should().Be(page.Items.Count);
    }

    [Test]
    public async Task CreateMediaSource_NewFolder_IsScannedAndFilterable()
    {
        string folder = await CreateFolderWithAudioAsync("Clip.mp3");

        HttpResponseMessage response = await _admin.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = Name("Videos"), path = folder });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        MediaSourceDto created = (await response.Content.ReadFromJsonAsync<MediaSourceDto>(
            HttpClientExtensions.JsonOptions))!;
        response.Headers.Location!.ToString().Should().EndWith($"/api/v1/sources/{created.Id}");
        created.Path.Should().Be(folder);
        created.FollowsMediaPath.Should().BeFalse();
        await _admin.WaitForJobsIdleAsync();
        PageDto<TrackDto> tracks = await _user.GetJsonAsync<PageDto<TrackDto>>(
            $"/api/v1/tracks?sourceId={created.Id}");
        tracks.Items.Select(track => track.Title).Should().Equal("Clip");
        PageDto<TrackDto> first = await _user.GetJsonAsync<PageDto<TrackDto>>(
            $"/api/v1/tracks?sourceId={MediaSource.DefaultId}");
        first.Items.Select(track => track.Title).Should().Equal("Song");
        (await _user.GetJsonAsync<MediaSourceDto>($"/api/v1/sources/{created.Id}")).TrackCount.Should().Be(1);
    }

    [Test]
    public async Task CreateMediaSource_NameTakenWithAnotherCase_ReturnsConflict()
    {
        HttpResponseMessage response = await _admin.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = "MEDIA", path = await CreateFolderWithAudioAsync() });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "source.name_taken");
    }

    [Test]
    public async Task CreateMediaSource_FolderInsideAnotherSource_ReturnsConflict()
    {
        HttpResponseMessage response = await _admin.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = Name("Inner"), path = _media.Combine("Band") });

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "source.path_overlaps");
    }

    [Test]
    public async Task CreateMediaSource_MissingFolder_ReturnsUnprocessable()
    {
        HttpResponseMessage response = await _admin.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = Name("Missing"), path = _others.Combine("missing-" + HttpClientExtensions.Unique()) });

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "source.path_not_found");
    }

    [Test]
    public async Task CreateMediaSource_RelativePathOrNoName_ReturnsValidationProblem()
    {
        HttpResponseMessage relative = await _admin.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = Name("Relative"), path = "music" });
        HttpResponseMessage noName = await _admin.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = "   ", path = await CreateFolderWithAudioAsync() });

        (await relative.ShouldBeValidationProblemAsync()).Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Field = "path", Code = ValidationErrorCodeDto.InvalidFormat });
        (await noName.ShouldBeValidationProblemAsync()).Should().ContainSingle().Which.Field.Should().Be("name");
    }

    [Test]
    public async Task UpdateMediaSource_Deactivate_KeepsItsTracks()
    {
        MediaSourceDto source = await CreateSourceAsync("Archive.mp3");

        HttpResponseMessage response = await _admin.PatchAsJsonAsync(
            $"/api/v1/sources/{source.Id}",
            new { isActive = false });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        MediaSourceDto changed = (await response.Content.ReadFromJsonAsync<MediaSourceDto>(
            HttpClientExtensions.JsonOptions))!;
        changed.IsActive.Should().BeFalse();
        changed.IsWatched.Should().BeFalse();
        changed.TrackCount.Should().Be(1);
        PageDto<TrackDto> tracks = await _user.GetJsonAsync<PageDto<TrackDto>>(
            $"/api/v1/tracks?sourceId={source.Id}");
        tracks.Items.Select(track => track.Title).Should().Equal("Archive");
    }

    [Test]
    public async Task UpdateMediaSource_Rename_ChangesOnlyTheName()
    {
        MediaSourceDto source = await CreateSourceAsync("Renamed.mp3");
        string name = Name("Renamed");

        MediaSourceDto changed = await (await _admin.PatchAsJsonAsync(
                $"/api/v1/sources/{source.Id}",
                new { name }))
            .Content.ReadFromJsonAsync<MediaSourceDto>(HttpClientExtensions.JsonOptions)
            ?? throw new InvalidOperationException("No source.");

        changed.Name.Should().Be(name);
        changed.Path.Should().Be(source.Path);
        changed.IsActive.Should().BeTrue();
    }

    [Test]
    public async Task UpdateMediaSource_UnknownSource_ReturnsNotFound()
    {
        HttpResponseMessage response = await _admin.PatchAsJsonAsync(
            $"/api/v1/sources/{Guid.NewGuid()}",
            new { name = Name("Nobody") });

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "source.not_found");
    }

    [Test]
    public async Task DeleteMediaSource_Source_RemovesItWithItsTracks()
    {
        MediaSourceDto source = await CreateSourceAsync("Gone.mp3");

        HttpResponseMessage response = await _admin.DeleteAsync($"/api/v1/sources/{source.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        (await _user.GetAsync($"/api/v1/sources/{source.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        PageDto<TrackDto> tracks = await _user.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?q=Gone");
        tracks.Items.Should().BeEmpty();
        await (await _admin.DeleteAsync($"/api/v1/sources/{source.Id}"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "source.not_found");
    }

    [Test]
    public async Task ListAlbumsAndArtists_SourceFilter_OnlyContainsTheSource()
    {
        MediaSourceDto source = await CreateSourceAsync("Filter Band/Filter Album/01 - Filtered.mp3");

        PageDto<ArtistDto> artists = await _user.GetJsonAsync<PageDto<ArtistDto>>(
            $"/api/v1/artists?sourceId={source.Id}");
        PageDto<AlbumDto> albums = await _user.GetJsonAsync<PageDto<AlbumDto>>(
            $"/api/v1/albums?sourceId={source.Id}");
        PageDto<TrackDto> none = await _user.GetJsonAsync<PageDto<TrackDto>>(
            $"/api/v1/tracks?sourceId={Guid.NewGuid()}");

        artists.Items.Select(artist => artist.Name).Should().Equal("Filter Band");
        albums.Items.Select(album => album.Title).Should().Equal("Filter Album");
        none.Total.Should().Be(0);
    }

    private async Task<MediaSourceDto> CreateSourceAsync(string fileName)
    {
        string folder = await CreateFolderWithAudioAsync(fileName);
        HttpResponseMessage response = await _admin.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = Name("Source"), path = folder });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        await _admin.WaitForJobsIdleAsync();
        return (await response.Content.ReadFromJsonAsync<MediaSourceDto>(HttpClientExtensions.JsonOptions))!;
    }

    private async Task<string> CreateFolderWithAudioAsync(string fileName = "Track.mp3")
    {
        string folder = _others.CreateDirectory(HttpClientExtensions.Unique());
        await TestMediaGenerator.CreateAudioAsync(Path.Combine(folder, fileName), AudioFormat.Mp3);
        return folder;
    }

    private static string Name(string prefix)
    {
        return $"{prefix} {HttpClientExtensions.Unique()}";
    }
}
