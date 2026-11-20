using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Cover rules on all three levels through the API: the global rule of the library settings, the rule of an album and
/// the rule of a track, with fallback and preview. The album has a picture in its folder (wide), one song with a
/// picture file of its own (tall), which it always shows first unless its own rule says otherwise, one song with an
/// embedded picture (square), which follows the rules, and one song without a cover. The file watcher is off, so only
/// the API changes the library.
/// </summary>
[TestFixture]
[Category("Integration")]
public class CoverRulesApiTests
{
    private TemporaryDirectory _media = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _user = null!;
    private Guid _albumId;
    private Guid _songId;
    private Guid _plainId;
    private Guid _embeddedId;
    private byte[] _albumPicture = null!;
    private byte[] _songPicture = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        Directory.CreateDirectory(_media.Combine("Band/Album"));
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Band/Album/cover.jpg"), 200, 100);
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Band/Album/01 Song.jpg"), 100, 200);
        await AudioAsync("Band/Album/01 Song.mp3", "Song", "1");
        await AudioAsync("Band/Album/02 Plain.mp3", "Plain", "2");
        string embedded = _media.Combine("embedded.jpg");
        await TestMediaGenerator.CreateImageAsync(embedded, 150, 150);
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Band/Album/03 Embedded.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = "Embedded", Artist = "Band", Album = "Album", Track = "3" },
            embeddedCoverPath: embedded);
        File.Delete(embedded);
        _albumPicture = await File.ReadAllBytesAsync(_media.Combine("Band/Album/cover.jpg"));
        _songPicture = await File.ReadAllBytesAsync(_media.Combine("Band/Album/01 Song.jpg"));

        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_cover_rules",
            freshDatabase: true,
            mediaPath: _media.Path);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        UserDto user = await _admin.CreateUserAsync(new { name = $"Viewer {HttpClientExtensions.Unique()}" });
        _user = _factory.CreateAuthorizedClient((await _anonymous.CreateSessionAsync(user.Id)).Token);

        HttpResponseMessage settings = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/library",
            """{ "fileWatcherEnabled": false }""");
        settings.StatusCode.Should().Be(HttpStatusCode.OK, await settings.Content.ReadAsStringAsync());

        HttpResponseMessage response = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        (await _admin.WaitForScanAsync(queued.Id)).Status.Should().Be(JobRunStatusDto.Succeeded);

        IReadOnlyList<TrackDto> tracks =
            (await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100")).Items;
        _songId = tracks.Single(track => track.Title == "Song").Id;
        _plainId = tracks.Single(track => track.Title == "Plain").Id;
        _embeddedId = tracks.Single(track => track.Title == "Embedded").Id;
        _albumId = tracks.Single(track => track.Title == "Song").Album!.Id;
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

    /// <summary>
    /// Every test starts with the defaults: no rules of albums and tracks, the album cover first globally.
    /// </summary>
    [SetUp]
    public async Task SetUp()
    {
        await SetGlobalAsync("AlbumFirst");
        await SetRuleAsync($"/api/v1/albums/{_albumId}/cover-rule", "Inherit");
        await SetRuleAsync($"/api/v1/tracks/{_songId}/cover-rule", "Inherit");
        await SetRuleAsync($"/api/v1/tracks/{_plainId}/cover-rule", "Inherit");
        await SetRuleAsync($"/api/v1/tracks/{_embeddedId}/cover-rule", "Inherit");
        await _admin.WaitForJobsIdleAsync();
    }

    [TestCase("GET", "albums", "cover-rule")]
    [TestCase("PUT", "albums", "cover-rule")]
    [TestCase("GET", "albums", "cover-rule/preview?rule=TrackFirst")]
    [TestCase("GET", "tracks", "cover-rule")]
    [TestCase("PUT", "tracks", "cover-rule")]
    [TestCase("GET", "tracks", "cover-rule/preview?rule=TrackFirst")]
    public async Task CoverRule_WithoutTokenOrAsUser_IsRejected(string method, string owner, string path)
    {
        Guid id = owner == "albums" ? _albumId : _songId;
        string url = $"/api/v1/{owner}/{id}/{path}";
        string? body = method == "PUT" ? """{ "rule": "TrackFirst" }""" : null;

        await (await _anonymous.SendJsonAsync(method, url, body))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await (await _user.SendJsonAsync(method, url, body))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [TestCase("GET", "albums", "cover-rule", "album.not_found")]
    [TestCase("PUT", "albums", "cover-rule", "album.not_found")]
    [TestCase("GET", "albums", "cover-rule/preview?rule=Inherit", "album.not_found")]
    [TestCase("GET", "tracks", "cover-rule", "track.not_found")]
    [TestCase("PUT", "tracks", "cover-rule", "track.not_found")]
    [TestCase("GET", "tracks", "cover-rule/preview?rule=Inherit", "track.not_found")]
    public async Task CoverRule_UnknownAlbumOrTrack_ReturnsNotFound(
        string method,
        string owner,
        string path,
        string code)
    {
        string? body = method == "PUT" ? """{ "rule": "TrackFirst" }""" : null;

        HttpResponseMessage response =
            await _admin.SendJsonAsync(method, $"/api/v1/{owner}/{Guid.NewGuid()}/{path}", body);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, code);
    }

    [Test]
    public async Task GetCoverRule_Defaults_InheritTheGlobalRule()
    {
        CoverRuleDto album = await _admin.GetJsonAsync<CoverRuleDto>($"/api/v1/albums/{_albumId}/cover-rule");
        CoverRuleDto track = await _admin.GetJsonAsync<CoverRuleDto>($"/api/v1/tracks/{_songId}/cover-rule");

        album.Rule.Should().Be(CoverRuleOptionDto.Inherit);
        album.InheritedPriority.Should().Be(CoverPriorityDto.AlbumFirst);
        album.EffectivePriority.Should().Be(CoverPriorityDto.AlbumFirst);
        track.Should().BeEquivalentTo(album);
        (await TrackCoverAsync(_songId)).Should().Equal(_songPicture);
        (await TrackCoverAsync(_plainId)).Should().Equal(_albumPicture);
        (await ImageProbe.GetSizeAsync(await TrackCoverAsync(_embeddedId))).Should().Be((200, 100));
        (await AlbumCoverAsync()).Should().Equal(_albumPicture);
    }

    [Test]
    public async Task SetTrackCoverRule_TrackFirst_ShowsTheOwnCoverOfTheTrackOnly()
    {
        CoverRuleDto rule = await SetRuleAsync($"/api/v1/tracks/{_songId}/cover-rule", "TrackFirst");

        rule.Rule.Should().Be(CoverRuleOptionDto.TrackFirst);
        rule.InheritedPriority.Should().Be(CoverPriorityDto.AlbumFirst);
        rule.EffectivePriority.Should().Be(CoverPriorityDto.TrackFirst);
        (await _admin.GetJsonAsync<CoverRuleDto>($"/api/v1/tracks/{_songId}/cover-rule")).Should().BeEquivalentTo(rule);
        (await TrackCoverAsync(_songId)).Should().Equal(_songPicture);
        (await AlbumCoverAsync()).Should().Equal(_albumPicture);
    }

    [Test]
    public async Task SetTrackCoverRule_TrackFirstWithAnEmbeddedPicture_ShowsTheEmbeddedPicture()
    {
        await SetRuleAsync($"/api/v1/tracks/{_embeddedId}/cover-rule", "TrackFirst");

        (await ImageProbe.GetSizeAsync(await TrackCoverAsync(_embeddedId))).Should().Be((150, 150));
    }

    [Test]
    public async Task SetTrackCoverRule_AlbumFirst_ShowsTheAlbumCoverBeforeThePictureFileOfTheTrack()
    {
        await SetGlobalAsync("TrackFirst");
        await SetRuleAsync($"/api/v1/tracks/{_songId}/cover-rule", "AlbumFirst");

        (await TrackCoverAsync(_songId)).Should().Equal(_albumPicture);
    }

    [Test]
    public async Task SetTrackCoverRule_TrackFirstWithoutOwnCover_FallsBackToTheAlbumCover()
    {
        await SetRuleAsync($"/api/v1/tracks/{_plainId}/cover-rule", "TrackFirst");

        (await TrackCoverAsync(_plainId)).Should().Equal(_albumPicture);
    }

    [Test]
    public async Task SetAlbumCoverRule_TrackFirst_AppliesToTheAlbumAndItsTracksWithoutRule()
    {
        CoverRuleDto album = await SetRuleAsync($"/api/v1/albums/{_albumId}/cover-rule", "TrackFirst");
        CoverRuleDto track = await _admin.GetJsonAsync<CoverRuleDto>($"/api/v1/tracks/{_songId}/cover-rule");

        album.EffectivePriority.Should().Be(CoverPriorityDto.TrackFirst);
        track.Rule.Should().Be(CoverRuleOptionDto.Inherit);
        track.InheritedPriority.Should().Be(CoverPriorityDto.TrackFirst);
        (await TrackCoverAsync(_songId)).Should().Equal(_songPicture);
        (await AlbumCoverAsync()).Should().Equal(_songPicture);
        (await TrackCoverAsync(_plainId)).Should().Equal(_albumPicture);
    }

    [Test]
    public async Task SetTrackCoverRule_AgainstTheRuleOfTheAlbum_TheTrackWins()
    {
        await SetRuleAsync($"/api/v1/albums/{_albumId}/cover-rule", "TrackFirst");
        await SetRuleAsync($"/api/v1/tracks/{_songId}/cover-rule", "AlbumFirst");

        (await TrackCoverAsync(_songId)).Should().Equal(_albumPicture);
        (await AlbumCoverAsync()).Should().Equal(_songPicture);
    }

    [Test]
    public async Task UpdateLibrarySettings_GlobalTrackFirst_AppliesUnlessAnAlbumOverridesIt()
    {
        LibrarySettingsDto settings = await SetGlobalAsync("TrackFirst");

        settings.CoverPriority.Should().Be(CoverPriorityDto.TrackFirst);
        (await _admin.GetJsonAsync<LibrarySettingsDto>("/api/v1/settings/library")).CoverPriority
            .Should().Be(CoverPriorityDto.TrackFirst);
        (await ImageProbe.GetSizeAsync(await TrackCoverAsync(_embeddedId))).Should().Be((150, 150));

        await SetRuleAsync($"/api/v1/albums/{_albumId}/cover-rule", "AlbumFirst");

        // The rule of the album applies to its songs, but a picture file of a song still comes first for it.
        (await ImageProbe.GetSizeAsync(await TrackCoverAsync(_embeddedId))).Should().Be((200, 100));
        (await TrackCoverAsync(_songId)).Should().Equal(_songPicture);
        (await _admin.GetJsonAsync<CoverRuleDto>($"/api/v1/albums/{_albumId}/cover-rule")).InheritedPriority
            .Should().Be(CoverPriorityDto.TrackFirst);
    }

    [Test]
    public async Task UpdateLibrarySettings_UnknownCoverPriority_ReturnsBadRequest()
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/library",
            """{ "coverPriority": "Sideways" }""");

        await response.ShouldBeValidationProblemAsync();
    }

    [Test]
    public async Task GetCoverRulePreview_OtherRule_DeliversItsCoverWithoutChangingTheRule()
    {
        HttpResponseMessage track = await _admin.GetAsync(
            $"/api/v1/tracks/{_songId}/cover-rule/preview?rule=TrackFirst&size=96");
        HttpResponseMessage album = await _admin.GetAsync(
            $"/api/v1/albums/{_albumId}/cover-rule/preview?rule=TrackFirst");
        HttpResponseMessage inherit = await _admin.GetAsync(
            $"/api/v1/tracks/{_embeddedId}/cover-rule/preview?rule=Inherit");
        HttpResponseMessage albumFirst = await _admin.GetAsync(
            $"/api/v1/tracks/{_songId}/cover-rule/preview?rule=AlbumFirst");

        track.StatusCode.Should().Be(HttpStatusCode.OK);
        track.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        (await ImageProbe.GetSizeAsync(await track.Content.ReadAsByteArrayAsync())).Should().Be((48, 96));
        (await album.Content.ReadAsByteArrayAsync()).Should().Equal(_songPicture);
        (await ImageProbe.GetSizeAsync(await inherit.Content.ReadAsByteArrayAsync())).Should().Be((200, 100));
        (await albumFirst.Content.ReadAsByteArrayAsync()).Should().Equal(_albumPicture);
        (await _admin.GetJsonAsync<CoverRuleDto>($"/api/v1/tracks/{_songId}/cover-rule")).Rule
            .Should().Be(CoverRuleOptionDto.Inherit);
        (await TrackCoverAsync(_songId)).Should().Equal(_songPicture);
    }

    [TestCase("")]
    [TestCase("?rule=Sideways")]
    [TestCase("?rule=7")]
    [TestCase("?rule=TrackFirst&size=0")]
    public async Task GetCoverRulePreview_InvalidQuery_ReturnsBadRequest(string query)
    {
        HttpResponseMessage response = await _admin.GetAsync($"/api/v1/tracks/{_songId}/cover-rule/preview{query}");

        await response.ShouldBeValidationProblemAsync();
    }

    [TestCase("{}")]
    [TestCase("""{ "rule": null }""")]
    [TestCase("""{ "rule": "Sideways" }""")]
    [TestCase("""{ "rule": 1 }""")]
    public async Task SetCoverRule_MissingOrUnknownRule_ReturnsBadRequest(string body)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PUT",
            $"/api/v1/albums/{_albumId}/cover-rule",
            body);

        await response.ShouldBeValidationProblemAsync();
    }

    [Test]
    public async Task SetCoverRule_Change_StartsThePreparationOfTheCovers()
    {
        await SetRuleAsync($"/api/v1/tracks/{_songId}/cover-rule", "TrackFirst");
        await _admin.WaitForJobsIdleAsync();

        PageDto<JobRunDto> runs = await _admin.GetJsonAsync<PageDto<JobRunDto>>(
            "/api/v1/jobs/cover-cache/runs?trigger=CoverRule&limit=100");

        runs.Items.Should().Contain(run => run.Status == JobRunStatusDto.Succeeded);
        (await _admin.GetAsync($"/api/v1/tracks/{_songId}/cover?size=96")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<CoverRuleDto> SetRuleAsync(string path, string rule)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync("PUT", path, $$"""{ "rule": "{{rule}}" }""");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CoverRuleDto>(HttpClientExtensions.JsonOptions))!;
    }

    private async Task<LibrarySettingsDto> SetGlobalAsync(string priority)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/library",
            $$"""{ "coverPriority": "{{priority}}" }""");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<LibrarySettingsDto>(HttpClientExtensions.JsonOptions))!;
    }

    /// <summary>
    /// The cover of the track as stored, which every user sees.
    /// </summary>
    private async Task<byte[]> TrackCoverAsync(Guid trackId)
    {
        HttpResponseMessage response = await _user.GetAsync($"/api/v1/tracks/{trackId}/cover");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task<byte[]> AlbumCoverAsync()
    {
        HttpResponseMessage response = await _user.GetAsync($"/api/v1/albums/{_albumId}/cover");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task AudioAsync(string path, string title, string number)
    {
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine(path),
            AudioFormat.Mp3,
            new TestTags { Title = title, Artist = "Band", Album = "Album", Track = number });
    }
}
