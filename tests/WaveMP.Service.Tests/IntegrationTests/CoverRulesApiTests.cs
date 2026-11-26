using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Cover rules through the API: the global rules of songs and albums in the library settings and the exceptions of
/// albums and tracks, with fallback and preview. The album "Album" of "Band" has a picture in its folder (wide), one
/// song with a picture named after it (tall), one song with an embedded picture (square), which is a cover of the
/// album, and one song without a cover. "Band" has a picture of its own (wider), the album "Bare" of "Band" has no
/// cover; "Duo" has no picture, only the collage of its album "Covered", and an album "Empty" without cover. The file
/// watcher is off, so only the API changes the library.
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
    private Guid _bareId;
    private Guid _emptyId;
    private Guid _songId;
    private Guid _plainId;
    private Guid _embeddedId;
    private Guid _bareSongId;
    private byte[] _albumPicture = null!;
    private byte[] _songPicture = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        Directory.CreateDirectory(_media.Combine("Band/Album"));
        Directory.CreateDirectory(_media.Combine("Duo/Covered"));
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Band/artist.jpg"), 240, 80);
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Band/Album/cover.jpg"), 200, 100);
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Band/Album/01 Song.jpg"), 100, 200);
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Duo/Covered/cover.jpg"), 90, 90);
        await AudioAsync("Band/Album/01 Song.mp3", "Band", "Album", "Song", "1");
        await AudioAsync("Band/Album/02 Plain.mp3", "Band", "Album", "Plain", "2");
        await AudioAsync("Band/Bare/01 Bare.mp3", "Band", "Bare", "Bare Song", "1");
        await AudioAsync("Duo/Covered/01 Hit.mp3", "Duo", "Covered", "Hit", "1");
        await AudioAsync("Duo/Empty/01 Miss.mp3", "Duo", "Empty", "Miss", "1");
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
        _bareSongId = tracks.Single(track => track.Title == "Bare Song").Id;
        _albumId = tracks.Single(track => track.Title == "Song").Album!.Id;
        _bareId = tracks.Single(track => track.Title == "Bare Song").Album!.Id;
        _emptyId = tracks.Single(track => track.Title == "Miss").Album!.Id;
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
    /// Every test starts with the defaults: no exceptions of albums and tracks, own cover first for songs, the picture
    /// of the artist for albums without a cover.
    /// </summary>
    [SetUp]
    public async Task SetUp()
    {
        await SetGlobalAsync("""{ "coverPriority": "TrackFirst", "albumCoverFallback": "ArtistPicture" }""");
        await SetRuleAsync($"/api/v1/albums/{_albumId}/cover-rule", "Inherit");
        await SetFallbackAsync(_bareId, "Inherit");
        foreach (Guid trackId in new[] { _songId, _plainId, _embeddedId })
        {
            await SetRuleAsync($"/api/v1/tracks/{trackId}/cover-rule", "Inherit");
        }

        await _admin.WaitForJobsIdleAsync();
    }

    [TestCase("GET", "albums", "cover-rule")]
    [TestCase("PUT", "albums", "cover-rule")]
    [TestCase("GET", "albums", "cover-rule/preview?rule=TrackFirst")]
    [TestCase("GET", "albums", "cover-fallback")]
    [TestCase("PUT", "albums", "cover-fallback")]
    [TestCase("GET", "tracks", "cover-rule")]
    [TestCase("PUT", "tracks", "cover-rule")]
    [TestCase("GET", "tracks", "cover-rule/preview?rule=TrackFirst")]
    public async Task CoverRule_WithoutTokenOrAsUser_IsRejected(string method, string owner, string path)
    {
        Guid id = owner == "albums" ? _albumId : _songId;
        string url = $"/api/v1/{owner}/{id}/{path}";
        string? body = method == "PUT" ? """{ "rule": "TrackFirst", "fallback": "None" }""" : null;

        await (await _anonymous.SendJsonAsync(method, url, body))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await (await _user.SendJsonAsync(method, url, body))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [TestCase("GET", "albums", "cover-rule", "album.not_found")]
    [TestCase("PUT", "albums", "cover-rule", "album.not_found")]
    [TestCase("GET", "albums", "cover-rule/preview?rule=Inherit", "album.not_found")]
    [TestCase("GET", "albums", "cover-fallback", "album.not_found")]
    [TestCase("PUT", "albums", "cover-fallback", "album.not_found")]
    [TestCase("GET", "tracks", "cover-rule", "track.not_found")]
    [TestCase("PUT", "tracks", "cover-rule", "track.not_found")]
    [TestCase("GET", "tracks", "cover-rule/preview?rule=Inherit", "track.not_found")]
    public async Task CoverRule_UnknownAlbumOrTrack_ReturnsNotFound(
        string method,
        string owner,
        string path,
        string code)
    {
        string? body = method == "PUT" ? """{ "rule": "TrackFirst", "fallback": "None" }""" : null;

        HttpResponseMessage response =
            await _admin.SendJsonAsync(method, $"/api/v1/{owner}/{Guid.NewGuid()}/{path}", body);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, code);
    }

    [Test]
    public async Task Defaults_SongsShowTheirOwnCoverElseTheAlbumCover()
    {
        CoverRuleDto album = await _admin.GetJsonAsync<CoverRuleDto>($"/api/v1/albums/{_albumId}/cover-rule");
        CoverRuleDto track = await _admin.GetJsonAsync<CoverRuleDto>($"/api/v1/tracks/{_songId}/cover-rule");
        LibrarySettingsDto settings = await _admin.GetJsonAsync<LibrarySettingsDto>("/api/v1/settings/library");

        settings.CoverPriority.Should().Be(CoverPriorityDto.TrackFirst);
        settings.AlbumCoverFallback.Should().Be(AlbumCoverFallbackDto.ArtistPicture);
        album.Rule.Should().Be(CoverRuleOptionDto.Inherit);
        album.InheritedPriority.Should().Be(CoverPriorityDto.TrackFirst);
        album.EffectivePriority.Should().Be(CoverPriorityDto.TrackFirst);
        track.Should().BeEquivalentTo(album);
        (await TrackCoverAsync(_songId)).Should().Equal(_songPicture);
        (await TrackCoverAsync(_plainId)).Should().Equal(_albumPicture);
        // The picture embedded in a song is a cover of the album; the picture in its folder comes first.
        (await TrackCoverAsync(_embeddedId)).Should().Equal(_albumPicture);
        (await AlbumCoverAsync(_albumId)).Should().Equal(_albumPicture);
    }

    [Test]
    public async Task GlobalAlbumFirst_SongsShowTheAlbumCoverElseTheirOwn()
    {
        await SetGlobalAsync("""{ "coverPriority": "AlbumFirst" }""");

        (await TrackCoverAsync(_songId)).Should().Equal(_albumPicture);
        (await TrackCoverAsync(_plainId)).Should().Equal(_albumPicture);
        (await AlbumCoverAsync(_albumId)).Should().Equal(_albumPicture);
    }

    [Test]
    public async Task GlobalTrackOnly_SongsShowOnlyTheirOwnCover()
    {
        await SetGlobalAsync("""{ "coverPriority": "TrackOnly" }""");

        (await TrackCoverAsync(_songId)).Should().Equal(_songPicture);
        await ShouldHaveNoCoverAsync($"/api/v1/tracks/{_plainId}/cover");
        await ShouldHaveNoCoverAsync($"/api/v1/tracks/{_embeddedId}/cover");
        (await _admin.GetJsonAsync<TrackDto>($"/api/v1/tracks/{_songId}")).HasCover.Should().BeTrue();
        (await AlbumCoverAsync(_albumId)).Should().Equal(_albumPicture);
    }

    [Test]
    public async Task AlbumWithoutCover_ShowsThePictureOfItsArtistButItsSongsNever()
    {
        byte[] cover = await AlbumCoverAsync(_bareId);
        AlbumDto album = await _admin.GetJsonAsync<AlbumDto>($"/api/v1/albums/{_bareId}");

        (await ImageProbe.GetSizeAsync(cover)).Should().Be((240, 80));
        album.HasCover.Should().BeTrue();
        // One level up only: a song shows the cover of its album, never the picture of the artist.
        await ShouldHaveNoCoverAsync($"/api/v1/tracks/{_bareSongId}/cover");
        (await _admin.GetJsonAsync<TrackDto>($"/api/v1/tracks/{_bareSongId}")).HasCover.Should().BeFalse();
    }

    [Test]
    public async Task AlbumWithoutCover_ArtistWithOnlyACollage_ShowsNoCover()
    {
        AlbumDto album = await _admin.GetJsonAsync<AlbumDto>($"/api/v1/albums/{_emptyId}");

        // The collage of the album covers of an artist is no picture of the artist.
        await ShouldHaveNoCoverAsync($"/api/v1/albums/{_emptyId}/cover");
        album.HasCover.Should().BeFalse();
    }

    [Test]
    public async Task GlobalFallbackNone_AlbumWithoutCoverShowsNothingUnlessItsExceptionSaysOtherwise()
    {
        LibrarySettingsDto settings = await SetGlobalAsync("""{ "albumCoverFallback": "None" }""");

        settings.AlbumCoverFallback.Should().Be(AlbumCoverFallbackDto.None);
        await ShouldHaveNoCoverAsync($"/api/v1/albums/{_bareId}/cover");
        (await AlbumCoverAsync(_albumId)).Should().Equal(_albumPicture);

        AlbumCoverFallbackRuleDto rule = await SetFallbackAsync(_bareId, "ArtistPicture");

        rule.Fallback.Should().Be(AlbumCoverFallbackOptionDto.ArtistPicture);
        rule.InheritedFallback.Should().Be(AlbumCoverFallbackDto.None);
        rule.EffectiveFallback.Should().Be(AlbumCoverFallbackDto.ArtistPicture);
        (await ImageProbe.GetSizeAsync(await AlbumCoverAsync(_bareId))).Should().Be((240, 80));
    }

    [Test]
    public async Task AlbumFallbackNone_HidesThePictureOfTheArtistForThisAlbumOnly()
    {
        AlbumCoverFallbackRuleDto inherited =
            await _admin.GetJsonAsync<AlbumCoverFallbackRuleDto>($"/api/v1/albums/{_bareId}/cover-fallback");

        AlbumCoverFallbackRuleDto rule = await SetFallbackAsync(_bareId, "None");

        inherited.Fallback.Should().Be(AlbumCoverFallbackOptionDto.Inherit);
        inherited.EffectiveFallback.Should().Be(AlbumCoverFallbackDto.ArtistPicture);
        rule.Fallback.Should().Be(AlbumCoverFallbackOptionDto.None);
        rule.EffectiveFallback.Should().Be(AlbumCoverFallbackDto.None);
        (await _admin.GetJsonAsync<AlbumCoverFallbackRuleDto>($"/api/v1/albums/{_bareId}/cover-fallback"))
            .Should().BeEquivalentTo(rule);
        await ShouldHaveNoCoverAsync($"/api/v1/albums/{_bareId}/cover");
        (await _admin.GetJsonAsync<AlbumDto>($"/api/v1/albums/{_bareId}")).HasCover.Should().BeFalse();
    }

    [Test]
    public async Task SetAlbumCoverRule_AlbumFirst_AppliesToItsSongsButNotToTheCoverOfTheAlbum()
    {
        CoverRuleDto album = await SetRuleAsync($"/api/v1/albums/{_albumId}/cover-rule", "AlbumFirst");
        CoverRuleDto track = await _admin.GetJsonAsync<CoverRuleDto>($"/api/v1/tracks/{_songId}/cover-rule");

        album.EffectivePriority.Should().Be(CoverPriorityDto.AlbumFirst);
        track.Rule.Should().Be(CoverRuleOptionDto.Inherit);
        track.InheritedPriority.Should().Be(CoverPriorityDto.AlbumFirst);
        (await TrackCoverAsync(_songId)).Should().Equal(_albumPicture);
        (await AlbumCoverAsync(_albumId)).Should().Equal(_albumPicture);
    }

    [Test]
    public async Task SetTrackCoverRule_AgainstTheRuleOfTheAlbum_TheTrackWins()
    {
        await SetRuleAsync($"/api/v1/albums/{_albumId}/cover-rule", "AlbumFirst");
        CoverRuleDto rule = await SetRuleAsync($"/api/v1/tracks/{_songId}/cover-rule", "TrackOnly");

        rule.Rule.Should().Be(CoverRuleOptionDto.TrackOnly);
        rule.InheritedPriority.Should().Be(CoverPriorityDto.AlbumFirst);
        rule.EffectivePriority.Should().Be(CoverPriorityDto.TrackOnly);
        (await TrackCoverAsync(_songId)).Should().Equal(_songPicture);
        (await TrackCoverAsync(_plainId)).Should().Equal(_albumPicture);
    }

    [Test]
    public async Task SetTrackCoverRule_TrackOnlyWithoutOwnCover_ShowsNoCover()
    {
        await SetRuleAsync($"/api/v1/tracks/{_plainId}/cover-rule", "TrackOnly");

        await ShouldHaveNoCoverAsync($"/api/v1/tracks/{_plainId}/cover");
    }

    [Test]
    public async Task UpdateLibrarySettings_UnknownCoverRules_ReturnBadRequest()
    {
        await (await _admin.SendJsonAsync("PATCH", "/api/v1/settings/library", """{ "coverPriority": "Sideways" }"""))
            .ShouldBeValidationProblemAsync();
        await (await _admin.SendJsonAsync("PATCH", "/api/v1/settings/library", """{ "albumCoverFallback": "Up" }"""))
            .ShouldBeValidationProblemAsync();
    }

    [Test]
    public async Task GetCoverRulePreview_OtherRule_DeliversItsCoverWithoutChangingTheRule()
    {
        HttpResponseMessage track = await _admin.GetAsync(
            $"/api/v1/tracks/{_songId}/cover-rule/preview?rule=TrackFirst&size=96");
        HttpResponseMessage albumFirst = await _admin.GetAsync(
            $"/api/v1/tracks/{_songId}/cover-rule/preview?rule=AlbumFirst");
        HttpResponseMessage trackOnly = await _admin.GetAsync(
            $"/api/v1/tracks/{_plainId}/cover-rule/preview?rule=TrackOnly");
        HttpResponseMessage album = await _admin.GetAsync(
            $"/api/v1/albums/{_albumId}/cover-rule/preview?rule=TrackOnly");

        track.StatusCode.Should().Be(HttpStatusCode.OK);
        track.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        (await ImageProbe.GetSizeAsync(await track.Content.ReadAsByteArrayAsync())).Should().Be((48, 96));
        (await albumFirst.Content.ReadAsByteArrayAsync()).Should().Equal(_albumPicture);
        trackOnly.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await album.Content.ReadAsByteArrayAsync()).Should().Equal(_albumPicture);
        (await _admin.GetJsonAsync<CoverRuleDto>($"/api/v1/tracks/{_songId}/cover-rule")).Rule
            .Should().Be(CoverRuleOptionDto.Inherit);
        (await TrackCoverAsync(_plainId)).Should().Equal(_albumPicture);
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

    [TestCase("cover-rule", "{}")]
    [TestCase("cover-rule", """{ "rule": null }""")]
    [TestCase("cover-rule", """{ "rule": "Sideways" }""")]
    [TestCase("cover-rule", """{ "rule": 1 }""")]
    [TestCase("cover-fallback", "{}")]
    [TestCase("cover-fallback", """{ "fallback": null }""")]
    [TestCase("cover-fallback", """{ "fallback": "Collage" }""")]
    [TestCase("cover-fallback", """{ "fallback": 1 }""")]
    public async Task SetAlbumRule_MissingOrUnknownValue_ReturnsBadRequest(string path, string body)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync("PUT", $"/api/v1/albums/{_albumId}/{path}", body);

        await response.ShouldBeValidationProblemAsync();
    }

    [Test]
    public async Task SetCoverRule_Change_StartsThePreparationOfTheCovers()
    {
        await SetRuleAsync($"/api/v1/tracks/{_songId}/cover-rule", "AlbumFirst");
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

    private async Task<AlbumCoverFallbackRuleDto> SetFallbackAsync(Guid albumId, string fallback)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PUT",
            $"/api/v1/albums/{albumId}/cover-fallback",
            $$"""{ "fallback": "{{fallback}}" }""");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<AlbumCoverFallbackRuleDto>(HttpClientExtensions.JsonOptions))!;
    }

    private async Task<LibrarySettingsDto> SetGlobalAsync(string body)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync("PATCH", "/api/v1/settings/library", body);
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

    private async Task<byte[]> AlbumCoverAsync(Guid albumId)
    {
        HttpResponseMessage response = await _user.GetAsync($"/api/v1/albums/{albumId}/cover");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task ShouldHaveNoCoverAsync(string url)
    {
        await (await _user.GetAsync(url)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "cover.not_found");
    }

    private async Task AudioAsync(string path, string artist, string album, string title, string number)
    {
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine(path),
            AudioFormat.Mp3,
            new TestTags { Title = title, Artist = artist, Album = album, Track = number });
    }
}
