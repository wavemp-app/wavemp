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
/// The pictures of the artists with synthetic media made by ffmpeg: the picture in the artist folder, an uploaded
/// picture, an online picture (stored through the data layer, since the lookup comes later) and the collage of the
/// album covers, in this order. Each test works on artists of its own; the file watcher is off.
/// </summary>
[TestFixture]
[Category("Integration")]
public class ArtistPicturesApiTests
{
    private TemporaryDirectory _media = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _listener = null!;
    private byte[] _picture = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        await AudioAsync("Framed Band/Record/01.mp3", "Framed Band", "Record", "Framed One");
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Framed Band/artist.jpg"), 640, 480);
        await AudioAsync("Ladder Band/Steps/01.mp3", "Ladder Band", "Steps", "Ladder One");
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Ladder Band/Steps/cover.jpg"), 300, 300);
        await AudioAsync("Collage Band/Older/01.mp3", "Collage Band", "Older", "Collage One", 2001);
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Collage Band/Older/cover.jpg"), 300, 300);
        await AudioAsync("Collage Band/Newer/01.mp3", "Collage Band", "Newer", "Collage Two", 2020);
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Collage Band/Newer/cover.png"), 300, 300);
        await AudioAsync("Collage Band/Plain/01.mp3", "Collage Band", "Plain", "Collage Three", 2024);
        await AudioAsync("Bare Band/Bare Album/01.mp3", "Bare Band", "Bare Album", "Bare One");
        await AudioAsync("Upload Band/Demo/01.mp3", "Upload Band", "Demo", "Upload One");
        await AudioAsync("Limit Band/Demo/01.mp3", "Limit Band", "Demo", "Limit One");

        using (TemporaryDirectory upload = new())
        {
            await TestMediaGenerator.CreateImageAsync(upload.Combine("upload.png"), 400, 250);
            _picture = await File.ReadAllBytesAsync(upload.Combine("upload.png"));
        }

        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_artist_pictures",
            freshDatabase: true,
            mediaPath: _media.Path);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        UserDto user = await _admin.CreateUserAsync(new { name = $"Listener {HttpClientExtensions.Unique()}" });
        _listener = _factory.CreateAuthorizedClient((await _anonymous.CreateSessionAsync(user.Id)).Token);

        HttpResponseMessage settings = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/library",
            """{ "fileWatcherEnabled": false }""");
        settings.StatusCode.Should().Be(HttpStatusCode.OK, await settings.Content.ReadAsStringAsync());

        HttpResponseMessage response = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        (await _admin.WaitForScanAsync(queued.Id)).Status.Should().Be(JobRunStatusDto.Succeeded);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _listener?.Dispose();
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
        _media.Dispose();
    }

    private static IEnumerable<TestCaseData> AdminEndpoints()
    {
        string path = $"/api/v1/artists/{Guid.NewGuid()}";
        yield return new TestCaseData("GET", $"{path}/uploaded-picture").SetArgDisplayNames("GetArtistUploadedPicture");
        yield return new TestCaseData("PUT", $"{path}/uploaded-picture").SetArgDisplayNames("UploadArtistPicture");
        yield return new TestCaseData("DELETE", $"{path}/uploaded-picture")
            .SetArgDisplayNames("DeleteArtistUploadedPicture");
        yield return new TestCaseData("GET", $"{path}/online-picture").SetArgDisplayNames("GetArtistOnlinePicture");
        yield return new TestCaseData("DELETE", $"{path}/online-picture")
            .SetArgDisplayNames("DeleteArtistOnlinePicture");
    }

    [TestCaseSource(nameof(AdminEndpoints))]
    [TestCase("GET", "/api/v1/artists/0196a0a0-0000-7000-8000-000000000001/picture")]
    public async Task ArtistPictureEndpoints_WithoutToken_ReturnUnauthorized(string method, string path)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), path)
        {
            Content = method == "PUT" ? Upload(_picture) : null,
        };

        HttpResponseMessage response = await _anonymous.SendAsync(request);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [TestCaseSource(nameof(AdminEndpoints))]
    public async Task AdminEndpoints_AsListener_ReturnForbidden(string method, string path)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), path)
        {
            Content = method == "PUT" ? Upload(_picture) : null,
        };

        HttpResponseMessage response = await _listener.SendAsync(request);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Test]
    public async Task GetArtist_PictureInTheArtistFolder_ShowsItBeforeUploadedAndOnlinePictures()
    {
        ArtistDto artist = await ArtistAsync("Framed Band");
        artist.Picture.Should().BeEquivalentTo(new
        {
            Kind = ArtistPictureKindDto.Local,
            AlbumIds = Array.Empty<Guid>(),
            HasLocalPicture = true,
            HasUploadedPicture = false,
            HasOnlinePicture = false,
        });
        artist.Picture.Version.Should().StartWith("l-");

        HttpResponseMessage uploaded = await _admin.PutAsync(UploadedPath(artist.Id), Upload(_picture));
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        await _factory.AddOnlineArtistPictureAsync(artist.Id, _picture);

        ArtistDto stored = await _listener.GetJsonAsync<ArtistDto>($"/api/v1/artists/{artist.Id}");
        stored.Picture.Should().BeEquivalentTo(new
        {
            Kind = ArtistPictureKindDto.Local,
            artist.Picture.Version,
            HasLocalPicture = true,
            HasUploadedPicture = true,
            HasOnlinePicture = true,
        });
        HttpResponseMessage picture = await _listener.GetAsync($"/api/v1/artists/{artist.Id}/picture?size=300");
        picture.StatusCode.Should().Be(HttpStatusCode.OK);
        picture.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        (await ImageProbe.GetSizeAsync(await picture.Content.ReadAsByteArrayAsync())).Should().Be((300, 225));

        // The hidden uploaded picture stays available to admins.
        HttpResponseMessage hidden = await _admin.GetAsync($"{UploadedPath(artist.Id)}?size=96");
        hidden.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ImageProbe.GetSizeAsync(await hidden.Content.ReadAsByteArrayAsync())).Should().Be((96, 96));
    }

    [Test]
    public async Task ArtistPicture_EachSourceRemoved_ShowsTheNextOneInPriority()
    {
        ArtistDto artist = await ArtistAsync("Ladder Band");
        AlbumDto album = (await _admin.GetJsonAsync<PageDto<AlbumDto>>($"/api/v1/artists/{artist.Id}/albums"))
            .Items.Single();
        artist.Picture.Kind.Should().Be(ArtistPictureKindDto.Albums);

        Guid onlineId = await _factory.AddOnlineArtistPictureAsync(artist.Id, _picture);
        ArtistDto online = await ArtistAsync("Ladder Band");
        online.Picture.Should().BeEquivalentTo(new
        {
            Kind = ArtistPictureKindDto.Online,
            Version = $"o-{onlineId:N}",
            AlbumIds = Array.Empty<Guid>(),
            HasOnlinePicture = true,
            OnlineCredit = new ArtistPictureCreditDto
            {
                Provider = ArtistPictureProviderDto.Wikimedia,
                Author = "Jane Doe",
                License = "CC BY-SA 4.0",
                LicenseUrl = "https://creativecommons.org/licenses/by-sa/4.0/",
                SourceUrl = "https://commons.wikimedia.org/wiki/File:Example.jpg",
            },
        });

        HttpResponseMessage response = await _admin.PutAsync(UploadedPath(artist.Id), Upload(_picture));
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        ArtistDto uploaded = (await response.Content.ReadFromJsonAsync<ArtistDto>(HttpClientExtensions.JsonOptions))!;
        uploaded.Picture.Kind.Should().Be(ArtistPictureKindDto.Uploaded);
        uploaded.Picture.Version.Should().StartWith("u-");
        uploaded.Picture.OnlineCredit!.Author.Should().Be("Jane Doe", "the hidden online picture keeps its credit");
        (await PictureETagAsync(artist.Id)).Should().StartWith($"\"{uploaded.Picture.Version}-");

        (await _admin.DeleteAsync(UploadedPath(artist.Id))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await ArtistAsync("Ladder Band")).Picture.Kind.Should().Be(ArtistPictureKindDto.Online);
        (await PictureETagAsync(artist.Id)).Should().StartWith($"\"o-{onlineId:N}-");
        (await _admin.GetAsync($"/api/v1/artists/{artist.Id}/online-picture")).StatusCode
            .Should().Be(HttpStatusCode.OK);

        (await _admin.DeleteAsync(OnlinePath(artist.Id))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        ArtistDto collage = await ArtistAsync("Ladder Band");
        collage.Picture.Should().BeEquivalentTo(new
        {
            Kind = ArtistPictureKindDto.Albums,
            Version = (string?)null,
            AlbumIds = new[] { album.Id },
            HasUploadedPicture = false,
            HasOnlinePicture = false,
            OnlineCredit = (ArtistPictureCreditDto?)null,
        });
        await (await _listener.GetAsync($"/api/v1/artists/{artist.Id}/picture"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "artist.picture_not_found");
        await (await _admin.GetAsync($"/api/v1/artists/{artist.Id}/online-picture"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "artist.picture_not_found");
        Directory.GetDirectories(Path.Combine(_factory.DataPath, "artist-pictures"))
            .Select(Path.GetFileName)
            .Should().NotContain([onlineId.ToString("N"), uploaded.Picture.Version![2..]]);
    }

    [Test]
    public async Task ListArtists_WithoutPicture_NameTheAlbumsWithCoverNewestFirstOrNothing()
    {
        ArtistDto collage = await ArtistAsync("Collage Band");
        PageDto<AlbumDto> albums = await _admin.GetJsonAsync<PageDto<AlbumDto>>(
            $"/api/v1/artists/{collage.Id}/albums?limit=100");
        Guid newer = albums.Items.Single(album => album.Title == "Newer").Id;
        Guid older = albums.Items.Single(album => album.Title == "Older").Id;

        collage.Picture.Kind.Should().Be(ArtistPictureKindDto.Albums);
        collage.Picture.AlbumIds.Should().Equal(newer, older);
        ArtistDto bare = await ArtistAsync("Bare Band");
        bare.Picture.Should().BeEquivalentTo(new
        {
            Kind = ArtistPictureKindDto.None,
            Version = (string?)null,
            AlbumIds = Array.Empty<Guid>(),
            HasLocalPicture = false,
        });
        await (await _listener.GetAsync($"/api/v1/artists/{bare.Id}/picture"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "artist.picture_not_found");
    }

    [Test]
    public async Task SearchLibrary_Artist_CarriesItsPicture()
    {
        SearchResultDto result = await _listener.GetJsonAsync<SearchResultDto>("/api/v1/search?q=Framed%20Band");

        result.Artists.Items.Should().Contain(artist =>
            artist.Name == "Framed Band" && artist.Picture.Kind == ArtistPictureKindDto.Local);
    }

    [Test]
    public async Task GetArtistPicture_MatchingETag_ReturnsNotModified()
    {
        ArtistDto artist = await ArtistAsync("Framed Band");
        HttpResponseMessage first = await _listener.GetAsync($"/api/v1/artists/{artist.Id}/picture?size=96");
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        first.Headers.CacheControl!.Private.Should().BeTrue();
        first.Headers.CacheControl.NoCache.Should().BeTrue();

        using HttpRequestMessage again = new(HttpMethod.Get, $"/api/v1/artists/{artist.Id}/picture?size=96");
        again.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        HttpResponseMessage second = await _listener.SendAsync(again);

        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Test]
    public async Task ArtistPictureEndpoints_UnknownArtist_ReturnArtistNotFound()
    {
        string path = $"/api/v1/artists/{Guid.NewGuid()}";

        await (await _listener.GetAsync($"{path}/picture")).ShouldBeProblemAsync(
            HttpStatusCode.NotFound,
            "artist.not_found");
        await (await _admin.PutAsync($"{path}/uploaded-picture", Upload(_picture))).ShouldBeProblemAsync(
            HttpStatusCode.NotFound,
            "artist.not_found");
        await (await _admin.DeleteAsync($"{path}/online-picture")).ShouldBeProblemAsync(
            HttpStatusCode.NotFound,
            "artist.not_found");
        await (await _admin.GetAsync($"{path}/uploaded-picture")).ShouldBeProblemAsync(
            HttpStatusCode.NotFound,
            "artist.not_found");
    }

    [Test]
    public async Task UploadArtistPicture_InvalidPictures_ReturnTheirProblemsAndStoreNothing()
    {
        ArtistDto artist = await ArtistAsync("Limit Band");
        string path = UploadedPath(artist.Id);
        byte[] damaged = [.. _picture.Take(64), .. new byte[2048]];
        byte[] large = [.. _picture, .. new byte[10 * 1024 * 1024]];

        await (await _admin.PutAsync(path, Upload("<svg><script/></svg>"u8.ToArray())))
            .ShouldBeProblemAsync(HttpStatusCode.UnsupportedMediaType, "artist.picture_unsupported_format");
        await (await _admin.PutAsync(path, Upload(damaged)))
            .ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "artist.picture_unreadable");
        await (await _admin.PutAsync(path, Upload(large)))
            .ShouldBeProblemAsync(HttpStatusCode.RequestEntityTooLarge, "artist.picture_too_large");
        MultipartFormDataContent empty = new() { { new StringContent("x"), "other" } };
        IReadOnlyList<ValidationErrorDto> errors =
            await (await _admin.PutAsync(path, empty)).ShouldBeValidationProblemAsync();

        errors.Should().ContainSingle(error => error.Field == "file" && error.Code == ValidationErrorCodeDto.Required);
        (await ArtistAsync("Limit Band")).Picture.HasUploadedPicture.Should().BeFalse();
    }

    [Test]
    public async Task GetArtistPicture_InvalidSize_ReturnsValidationProblem()
    {
        ArtistDto artist = await ArtistAsync("Framed Band");

        HttpResponseMessage response = await _listener.GetAsync($"/api/v1/artists/{artist.Id}/picture?size=0");

        await response.ShouldBeValidationProblemAsync();
    }

    private async Task<ArtistDto> ArtistAsync(string name)
    {
        PageDto<ArtistDto> artists = await _admin.GetJsonAsync<PageDto<ArtistDto>>("/api/v1/artists?limit=100");
        return artists.Items.Single(artist => artist.Name == name);
    }

    private async Task<string> PictureETagAsync(Guid artistId)
    {
        HttpResponseMessage response = await _listener.GetAsync($"/api/v1/artists/{artistId}/picture?size=96");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return response.Headers.ETag!.Tag;
    }

    private static string UploadedPath(Guid artistId)
    {
        return $"/api/v1/artists/{artistId}/uploaded-picture";
    }

    private static string OnlinePath(Guid artistId)
    {
        return $"/api/v1/artists/{artistId}/online-picture";
    }

    private static MultipartFormDataContent Upload(byte[] picture)
    {
        ByteArrayContent file = new(picture);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { file, "file", "picture.png" } };
    }

    private Task AudioAsync(string path, string artist, string album, string title, int? year = null)
    {
        return TestMediaGenerator.CreateAudioAsync(
            _media.Combine(path),
            AudioFormat.Mp3,
            new TestTags
            {
                Artist = artist,
                Album = album,
                Title = title,
                Date = year?.ToString(CultureInfo.InvariantCulture),
            });
    }
}
