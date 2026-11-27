using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WaveMP.Core.OnlineMetadata.Clients;
using WaveMP.Data;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Searches album info online and applies it through the API, with simulated MusicBrainz and Cover Art Archive; no
/// request leaves the process. Every test sets the switch of the online search it needs.
/// </summary>
[TestFixture]
[Category("Integration")]
public class OnlineMetadataApiTests
{
    private static readonly Guid _releaseId = Guid.Parse("7a0c3d43-1d7e-4b0e-9a69-2c6dd1b46a01");
    private static readonly Guid _releaseWithoutCover = Guid.Parse("7a0c3d43-1d7e-4b0e-9a69-2c6dd1b46a02");
    private static readonly Guid _otherRelease = Guid.Parse("7a0c3d43-1d7e-4b0e-9a69-2c6dd1b46a03");

    private static readonly TestTags _album = new() { Artist = "Band", Album = "Album", Date = "2001" };

    private TemporaryDirectory _media = null!;
    private TemporaryDirectory _pictures = null!;
    private byte[] _jpeg = null!;
    private byte[] _png = null!;
    private IMusicBrainzClient _musicBrainz = null!;
    private ICoverArtArchiveClient _coverArtArchive = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _user = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _pictures = new TemporaryDirectory();
        await TestMediaGenerator.CreateImageAsync(_pictures.Combine("front.jpg"), 64, 64);
        await TestMediaGenerator.CreateImageAsync(_pictures.Combine("online.png"), 32, 32);
        _jpeg = await File.ReadAllBytesAsync(_pictures.Combine("front.jpg"));
        _png = await File.ReadAllBytesAsync(_pictures.Combine("online.png"));

        _media = new TemporaryDirectory();
        await AudioAsync("Band/Album/01 - Song.mp3", _album with { Title = "Song", Track = "1/2" });
        await AudioAsync("Band/Album/02 - Other.mp3", _album with { Title = "Other", Track = "2/2" });
        await AudioAsync("Singles/Lonely.mp3", new TestTags { Title = "Lonely", Artist = "Solo" });
        await AudioAsync("Singles/Offline.mp3", new TestTags { Title = "Offline", Artist = "Solo" });
        await AudioAsync("Locked/Locked.mp3", new TestTags { Title = "Locked", Artist = "Solo" });
        await AudioAsync("Shared/01 First.mp3", new TestTags { Title = "First", Artist = "Duo", Album = "One" });
        await AudioAsync("Shared/02 Second.mp3", new TestTags { Title = "Second", Artist = "Duo", Album = "Two" });
        File.SetAttributes(_media.Combine("Locked/Locked.mp3"), FileAttributes.ReadOnly);

        _musicBrainz = Substitute.For<IMusicBrainzClient>();
        _musicBrainz.SearchRecordingsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<IReadOnlyList<MusicBrainzRecording>>.Found([Recording()]));
        _musicBrainz.SearchRecordingsAsync(
                Arg.Is<string>(query => query.Contains("Offline", StringComparison.Ordinal)),
                Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<IReadOnlyList<MusicBrainzRecording>>.Failed(OnlineStatus.Unavailable));
        _musicBrainz.SearchReleasesAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<IReadOnlyList<MusicBrainzRelease>>.Found([Release()]));
        _coverArtArchive = Substitute.For<ICoverArtArchiveClient>();
        _coverArtArchive.GetFrontCoverAsync(_releaseId, Arg.Any<CoverArtSize>(), Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<byte[]>.Found(_jpeg));
        _coverArtArchive.GetFrontCoverAsync(_releaseWithoutCover, Arg.Any<CoverArtSize>(), Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<byte[]>.Failed(OnlineStatus.NotFound));
        _coverArtArchive.GetFrontCoverAsync(_otherRelease, Arg.Any<CoverArtSize>(), Arg.Any<CancellationToken>())
            .Returns(OnlineResponse<byte[]>.Found(_png));

        _factory = new WaveMpWebApplicationFactory(
            "wavemp_tests_service_online_metadata",
            freshDatabase: true,
            mediaPath: _media.Path,
            musicBrainz: _musicBrainz,
            coverArtArchive: _coverArtArchive);
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

        await ScanAsync();
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
        _pictures.Dispose();
    }

    [Test]
    public async Task GetOnlineMetadataSettings_Defaults_AreOffWithTheOfficialServices()
    {
        await using WaveMpWebApplicationFactory fresh = new(
            "wavemp_tests_service_online_metadata_defaults",
            freshDatabase: true);
        using HttpClient admin = await fresh.CreateAdminClientAsync();

        OnlineMetadataSettingsDto settings =
            await admin.GetJsonAsync<OnlineMetadataSettingsDto>("/api/v1/settings/online-metadata");

        settings.Should().BeEquivalentTo(new OnlineMetadataSettingsDto
        {
            Enabled = false,
            MusicBrainzUrl = "https://musicbrainz.org",
            CoverArtArchiveUrl = "https://coverartarchive.org",
            MusicBrainzRequestIntervalMilliseconds = 1000,
        });
        await fresh.DeleteDatabaseAsync();
    }

    [Test]
    public async Task UpdateOnlineMetadataSettings_Mirror_SavesTheNormalizedAddress()
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/online-metadata",
            """{ "musicBrainzUrl": "http://mirror.local:5000/", "musicBrainzRequestIntervalMilliseconds": 0 }""");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        OnlineMetadataSettingsDto settings =
            await _admin.GetJsonAsync<OnlineMetadataSettingsDto>("/api/v1/settings/online-metadata");
        settings.MusicBrainzUrl.Should().Be("http://mirror.local:5000");
        settings.MusicBrainzRequestIntervalMilliseconds.Should().Be(0);

        await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/online-metadata",
            """{ "musicBrainzUrl": "https://musicbrainz.org", "musicBrainzRequestIntervalMilliseconds": 1000 }""");
    }

    [Test]
    public async Task UpdateOnlineMetadataSettings_InvalidAddresses_ReturnsInvalidFormatPerField()
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/online-metadata",
            """{ "musicBrainzUrl": "musicbrainz.org", "coverArtArchiveUrl": "ftp://covers" }""");

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Select(error => (error.Field, error.Code)).Should().BeEquivalentTo(
        [
            ("musicBrainzUrl", ValidationErrorCodeDto.InvalidFormat),
            ("coverArtArchiveUrl", ValidationErrorCodeDto.InvalidFormat),
        ]);
    }

    [Test]
    public async Task UpdateOnlineMetadataSettings_IntervalOutOfRange_ReturnsItsLimits()
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/online-metadata",
            """{ "musicBrainzRequestIntervalMilliseconds": 70000 }""");

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new ValidationErrorDto
        {
            Field = "musicBrainzRequestIntervalMilliseconds",
            Code = ValidationErrorCodeDto.OutOfRange,
            Min = 0,
            Max = 60_000,
        });
    }

    [TestCase("GET", "/api/v1/settings/online-metadata")]
    [TestCase("GET", "/api/v1/tracks/{track}/online-matches")]
    [TestCase("GET", "/api/v1/albums/{album}/online-matches")]
    [TestCase("GET", "/api/v1/online-releases/7a0c3d43-1d7e-4b0e-9a69-2c6dd1b46a01/cover")]
    [TestCase("POST", "/api/v1/tracks/{track}/online-metadata")]
    [TestCase("POST", "/api/v1/albums/{album}/online-metadata")]
    public async Task OnlineMetadata_AsUser_ReturnsForbidden(string method, string path)
    {
        TrackDto track = await TrackAsync("Song");
        HttpResponseMessage response = await _user.SendJsonAsync(
            method,
            path.Replace("{track}", track.Id.ToString(), StringComparison.Ordinal)
                .Replace("{album}", track.Album!.Id.ToString(), StringComparison.Ordinal),
            method == "POST" ? "{}" : null);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Test]
    public async Task OnlineMetadata_Disabled_SendsNoRequestToEitherService()
    {
        await SetEnabledAsync(false);
        _musicBrainz.ClearReceivedCalls();
        _coverArtArchive.ClearReceivedCalls();
        TrackDto track = await TrackAsync("Song");

        HttpResponseMessage[] responses =
        [
            await _admin.GetAsync($"/api/v1/tracks/{track.Id}/online-matches"),
            await _admin.GetAsync($"/api/v1/albums/{track.Album!.Id}/online-matches"),
            await _admin.GetAsync($"/api/v1/online-releases/{_releaseId}/cover"),
            await _admin.SendJsonAsync(
                "POST",
                $"/api/v1/tracks/{track.Id}/online-metadata",
                $$"""{ "coverReleaseId": "{{_releaseId}}" }"""),
            await _admin.SendJsonAsync(
                "POST",
                $"/api/v1/albums/{track.Album.Id}/online-metadata",
                $$"""{ "coverReleaseId": "{{_releaseId}}" }"""),
        ];

        foreach (HttpResponseMessage response in responses)
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "lookup.disabled");
        }

        _musicBrainz.ReceivedCalls().Should().BeEmpty();
        _coverArtArchive.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task SearchTrackOnlineMatches_Enabled_ReturnsMatchesNextToTheCurrentTags()
    {
        await SetEnabledAsync(true);
        TrackDto track = await TrackAsync("Other");

        OnlineTrackMatchesDto result =
            await _admin.GetJsonAsync<OnlineTrackMatchesDto>($"/api/v1/tracks/{track.Id}/online-matches");

        result.Current.Title.Should().Be("Other");
        result.Current.Status.Should().Be(TrackMetadataStatusDto.Editable);
        OnlineTrackMatchDto match = result.Matches.Should().ContainSingle().Subject;
        match.Release!.Id.Should().Be(_releaseId);
        match.Release.Country.Should().Be("GB");
        (match.Title, match.Artist, match.Album, match.Year, match.TrackNumber, match.Genre)
            .Should().Be(("Song", "Band", "Album", 1999, 1, "Rock"));
        match.Agreement.Should().BeInRange(0, 100);
        await _musicBrainz.Received().SearchRecordingsAsync(
            Arg.Is<string>(
                query => query.StartsWith("recording:\"Other\" AND artist:\"Band\"", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SearchTrackOnlineMatches_ServiceUnavailable_ReturnsServiceUnavailable()
    {
        await SetEnabledAsync(true);
        TrackDto track = await TrackAsync("Offline");

        HttpResponseMessage response = await _admin.GetAsync($"/api/v1/tracks/{track.Id}/online-matches");

        await response.ShouldBeProblemAsync(HttpStatusCode.ServiceUnavailable, "lookup.unavailable");
    }

    [Test]
    public async Task SearchTrackOnlineMatches_UnknownTrack_ReturnsNotFound()
    {
        await SetEnabledAsync(true);

        HttpResponseMessage response = await _admin.GetAsync($"/api/v1/tracks/{Guid.NewGuid()}/online-matches");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "track.not_found");
    }

    [Test]
    public async Task SearchAlbumOnlineMatches_Enabled_ReturnsReleasesWithTheCountOfEditableSongs()
    {
        await SetEnabledAsync(true);
        TrackDto track = await TrackAsync("Song");

        OnlineAlbumMatchesDto result = await _admin.GetJsonAsync<OnlineAlbumMatchesDto>(
            $"/api/v1/albums/{track.Album!.Id}/online-matches");

        result.Current.Album.Should().Be(track.Album.Title);
        result.TrackCount.Should().Be(2);
        result.EditableTrackCount.Should().Be(2);
        OnlineAlbumMatchDto match = result.Matches.Should().ContainSingle().Subject;
        match.Release.Id.Should().Be(_releaseId);
        match.Values.Album.Should().Be("Album");
    }

    [Test]
    public async Task GetOnlineReleaseCover_Picture_ReturnsItAndUnknownCoverIsNotFound()
    {
        await SetEnabledAsync(true);

        HttpResponseMessage found = await _admin.GetAsync($"/api/v1/online-releases/{_releaseId}/cover?size=Large");
        HttpResponseMessage missing = await _admin.GetAsync($"/api/v1/online-releases/{_releaseWithoutCover}/cover");

        found.StatusCode.Should().Be(HttpStatusCode.OK);
        found.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        (await found.Content.ReadAsByteArrayAsync()).Should().Equal(_jpeg);
        await _coverArtArchive.Received()
            .GetFrontCoverAsync(_releaseId, CoverArtSize.Large, Arg.Any<CancellationToken>());
        await missing.ShouldBeProblemAsync(HttpStatusCode.NotFound, "lookup.cover_not_found");
    }

    [Test]
    public async Task ApplyTrackOnlineMetadata_TagsAndCover_WritesTheFileWithBackupAndTheCoverOfTheSong()
    {
        await SetEnabledAsync(true);
        TrackDto track = await TrackAsync("Song");

        HttpResponseMessage response = await _admin.SendJsonAsync(
            "POST",
            $"/api/v1/tracks/{track.Id}/online-metadata",
            $$"""{ "tags": { "title": "Song (Remastered)", "year": 1999 }, "coverReleaseId": "{{_releaseId}}" }""");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        OnlineTrackApplyResultDto result =
            (await response.Content.ReadFromJsonAsync<OnlineTrackApplyResultDto>(HttpClientExtensions.JsonOptions))!;
        result.Metadata.Title.Should().Be("Song (Remastered)");
        result.Metadata.Year.Should().Be(1999);
        result.Cover.Should().Be(OnlineCoverOutcomeDto.MediaFolder);
        (await File.ReadAllBytesAsync(_media.Combine("Band/Album/01 - Song.jpg"))).Should().Equal(_jpeg);
        File.Exists(_media.Combine("Band/Album/cover.jpg")).Should().BeFalse();
        using (var file = TagLib.File.Create(_media.Combine("Band/Album/01 - Song.mp3")))
        {
            (file.Tag.Title, file.Tag.Year).Should().Be(("Song (Remastered)", 1999u));
        }

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        WaveMpDbContext db = scope.ServiceProvider.GetRequiredService<WaveMpDbContext>();
        (await db.TagBackups.AnyAsync(backup => backup.RelativePath == "Band/Album/01 - Song.mp3"))
            .Should().BeTrue();
        TrackDto other = await TrackAsync("Other");
        other.HasCover.Should().BeFalse("a song never shows the picture named after another song");
    }

    [Test]
    public async Task ApplyAlbumOnlineMetadata_TwoAlbumsInTheSameFolder_KeepTheirOwnCovers()
    {
        await SetEnabledAsync(true);
        Guid one = (await TrackAsync("First")).Album!.Id;
        Guid two = (await TrackAsync("Second")).Album!.Id;

        OnlineAlbumApplyResultDto first = await ApplyAlbumCoverAsync(one, _releaseId);
        OnlineAlbumApplyResultDto second = await ApplyAlbumCoverAsync(two, _otherRelease);

        // The covers are kept by the id of each album, never as a picture in the folder both albums share.
        (first.Cover, second.Cover).Should().Be((OnlineCoverOutcomeDto.DataFolder, OnlineCoverOutcomeDto.DataFolder));
        Directory.EnumerateFiles(_media.Combine("Shared")).Select(Path.GetFileName)
            .Should().BeEquivalentTo("01 First.mp3", "02 Second.mp3");
        (await (await _user.GetAsync($"/api/v1/albums/{one}/cover")).Content.ReadAsByteArrayAsync())
            .Should().Equal(_jpeg);
        (await (await _user.GetAsync($"/api/v1/albums/{two}/cover")).Content.ReadAsByteArrayAsync())
            .Should().Equal(_png);
        (await TrackAsync("First")).HasCover.Should().BeTrue();
    }

    [Test]
    public async Task ApplyTrackOnlineMetadata_NotWritable_WritesNeitherTagsNorCover()
    {
        await SetEnabledAsync(true);
        TrackDto track = await TrackAsync("Locked");

        HttpResponseMessage response = await _admin.SendJsonAsync(
            "POST",
            $"/api/v1/tracks/{track.Id}/online-metadata",
            $$"""{ "tags": { "title": "Unlocked" }, "coverReleaseId": "{{_releaseId}}" }""");

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "track.not_writable");
        Directory.EnumerateFiles(_media.Combine("Locked")).Select(Path.GetFileName).Should().Equal("Locked.mp3");
    }

    [Test]
    public async Task ApplyTrackOnlineMetadata_ReleaseWithoutCover_ChangesNothing()
    {
        await SetEnabledAsync(true);
        TrackDto track = await TrackAsync("Lonely");

        HttpResponseMessage response = await _admin.SendJsonAsync(
            "POST",
            $"/api/v1/tracks/{track.Id}/online-metadata",
            $$"""{ "tags": { "title": "Changed" }, "coverReleaseId": "{{_releaseWithoutCover}}" }""");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "lookup.cover_not_found");
        (await _admin.GetJsonAsync<TrackDto>($"/api/v1/tracks/{track.Id}")).Title.Should().Be("Lonely");
    }

    [Test]
    public async Task ApplyTrackOnlineMetadata_InvalidYear_ReturnsValidationProblem()
    {
        await SetEnabledAsync(true);
        TrackDto track = await TrackAsync("Lonely");

        HttpResponseMessage response = await _admin.SendJsonAsync(
            "POST",
            $"/api/v1/tracks/{track.Id}/online-metadata",
            """{ "tags": { "year": 99999 } }""");

        await response.ShouldBeValidationProblemAsync();
    }

    [Test]
    public async Task ApplyAlbumOnlineMetadata_Genre_WritesEverySongOfTheAlbum()
    {
        await SetEnabledAsync(true);
        TrackDto track = await TrackAsync("Other");

        HttpResponseMessage response = await _admin.SendJsonAsync(
            "POST",
            $"/api/v1/albums/{track.Album!.Id}/online-metadata",
            """{ "tags": { "genre": "Jazz", "title": "ignored" } }""");

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        OnlineAlbumApplyResultDto result =
            (await response.Content.ReadFromJsonAsync<OnlineAlbumApplyResultDto>(HttpClientExtensions.JsonOptions))!;
        (result.TracksWritten, result.TracksSkipped, result.Cover).Should().Be((2, 0, OnlineCoverOutcomeDto.None));
        result.AlbumId.Should().Be(track.Album.Id);
        (await TrackAsync("Other")).Genre.Should().Be("Jazz");
        (await TrackAsync("Other")).Title.Should().Be("Other");
    }

    [Test]
    public async Task ApplyAlbumOnlineMetadata_UnknownAlbum_ReturnsNotFound()
    {
        await SetEnabledAsync(true);

        HttpResponseMessage response = await _admin.SendJsonAsync(
            "POST",
            $"/api/v1/albums/{Guid.NewGuid()}/online-metadata",
            "{}");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "album.not_found");
    }

    [Test]
    public async Task OnlineCover_InTheDataFolder_IsTheCoverOfTheSong()
    {
        TrackDto track = await TrackAsync("Lonely");
        var id = Guid.CreateVersion7();
        string fileName = $"{id:N}.png";
        string folder = Path.Combine(_factory.DataPath, "online-covers");
        Directory.CreateDirectory(folder);
        await File.WriteAllBytesAsync(Path.Combine(folder, fileName), _png);
        await using (AsyncServiceScope scope = _factory.Services.CreateAsyncScope())
        {
            WaveMpDbContext db = scope.ServiceProvider.GetRequiredService<WaveMpDbContext>();
            db.OnlineCovers.Add(new OnlineCover
            {
                Id = id,
                TrackId = track.Id,
                FileName = fileName,
                MimeType = "image/png",
                Size = _png.Length,
                ReleaseId = _releaseId,
                CreatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        TrackDto reloaded = await _admin.GetJsonAsync<TrackDto>($"/api/v1/tracks/{track.Id}");
        HttpResponseMessage cover = await _admin.GetAsync($"/api/v1/tracks/{track.Id}/cover?size=96");

        reloaded.HasCover.Should().BeTrue();
        cover.StatusCode.Should().Be(HttpStatusCode.OK, await cover.Content.ReadAsStringAsync());
        cover.Content.Headers.ContentType!.MediaType.Should().StartWith("image/");
    }

    private async Task<OnlineAlbumApplyResultDto> ApplyAlbumCoverAsync(Guid albumId, Guid releaseId)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "POST",
            $"/api/v1/albums/{albumId}/online-metadata",
            $$"""{ "tags": {}, "coverReleaseId": "{{releaseId}}" }""");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<OnlineAlbumApplyResultDto>(HttpClientExtensions.JsonOptions))!;
    }

    private async Task SetEnabledAsync(bool enabled)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/online-metadata",
            enabled ? """{ "enabled": true }""" : """{ "enabled": false }""");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task<TrackDto> TrackAsync(string title)
    {
        IReadOnlyList<TrackDto> tracks =
            (await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100")).Items;
        return tracks.Single(track => track.Title == title
            || (title == "Song" && track.Title.StartsWith("Song", StringComparison.Ordinal)));
    }

    private async Task ScanAsync()
    {
        HttpResponseMessage response = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        ScanDto scan = await _admin.WaitForScanAsync(queued.Id);
        scan.Status.Should().Be(JobRunStatusDto.Succeeded);
    }

    private Task AudioAsync(string relativePath, TestTags tags)
    {
        return TestMediaGenerator.CreateAudioAsync(_media.Combine(relativePath), AudioFormat.Mp3, tags);
    }

    private static MusicBrainzRecording Recording()
    {
        return new MusicBrainzRecording
        {
            Id = Guid.NewGuid(),
            Score = 100,
            Title = "Song",
            Length = 3000,
            ArtistCredit = [new MusicBrainzArtistCredit { Name = "Band" }],
            Tags = [new MusicBrainzTag { Name = "rock", Count = 2 }],
            Releases = [Release()],
        };
    }

    private static MusicBrainzRelease Release()
    {
        return new MusicBrainzRelease
        {
            Id = _releaseId,
            Score = 100,
            Title = "Album",
            Date = "1999-03-01",
            Country = "GB",
            TrackCount = 2,
            ArtistCredit = [new MusicBrainzArtistCredit { Name = "Band" }],
            Media =
            [
                new MusicBrainzMedium
                {
                    Position = 1,
                    Format = "CD",
                    Tracks = [new MusicBrainzTrack { Number = "1", Title = "Song" }],
                },
            ],
        };
    }
}
