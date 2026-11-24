using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Http;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// The online lookup of artist pictures through the API with simulated MusicBrainz, Wikidata, Wikimedia Commons and
/// fanart.tv: the HTTP handler of the lookup is replaced, so no request leaves the process, and every request the
/// server sends is recorded. The lookup is off at the start of every test.
/// </summary>
[TestFixture]
[Category("Integration")]
public class ArtistPictureLookupApiTests
{
    private const string SettingsPath = "/api/v1/settings/artist-pictures";
    private const string ApiKey = "a1b2c3d4e5f60718293a4b5c6d7e8f90";
    private const string MusicBrainz = "https://musicbrainz.org/ws/2/artist";
    private const string Wikidata = "https://www.wikidata.org/w/api.php?action=wbgetentities&ids=Q4242";
    private const string Commons = "https://commons.wikimedia.org/w/api.php?";
    private const string PreviewUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/a/ab/Band.png/500px-Band.png";
    private const string FullUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/a/ab/Band.png/1280px-Band.png";
    private const string FullTarget = "https://upload.wikimedia.org/wikipedia/commons/a/ab/Band.png";
    private const string FanartThumb = "https://assets.fanart.tv/fanart/music/x/artistthumb/band.png";
    private const string FanartTrap = "https://assets.fanart.tv/fanart/music/x/artistthumb/trap.png";

    private static readonly Guid _musicBrainzId = Guid.Parse("0c0c0c0c-1d1d-4e4e-8f8f-a0a0a0a0a0a0");

    private TemporaryDirectory _media = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _listener = null!;
    private byte[] _png = null!;

    private SimulatedWebHandler Web => _factory.ArtistLookups;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        await AudioAsync("Lookup Band/Demo/01.mp3", "Lookup Band");
        await AudioAsync("Fanart Band/Demo/01.mp3", "Fanart Band");
        await AudioAsync("Silent Band/Demo/01.mp3", "Silent Band");
        using (TemporaryDirectory pictures = new())
        {
            await TestMediaGenerator.CreateImageAsync(pictures.Combine("band.png"), 600, 400);
            _png = await File.ReadAllBytesAsync(pictures.Combine("band.png"));
        }

        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_artist_picture_lookups",
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

    [SetUp]
    public async Task SetUp()
    {
        await SwitchAsync("""{ "onlineLookupEnabled": false, "fanartTvApiKey": "" }""");
        Web.Reset();
        GivenServices();
    }

    private static IEnumerable<TestCaseData> LookupEndpoints()
    {
        string path = $"/api/v1/artists/{Guid.NewGuid()}";
        string candidate = new('a', 32);
        yield return new TestCaseData("GET", $"{path}/musicbrainz-matches")
            .SetArgDisplayNames("ListArtistMusicBrainzMatches");
        yield return new TestCaseData("GET", $"{path}/online-picture-candidates?musicBrainzArtistId={_musicBrainzId}")
            .SetArgDisplayNames("ListArtistPictureCandidates");
        yield return new TestCaseData("GET", $"{path}/online-picture-candidates/{candidate}/preview")
            .SetArgDisplayNames("GetArtistPictureCandidatePreview");
        yield return new TestCaseData("PUT", $"{path}/online-picture").SetArgDisplayNames("TakeOverArtistOnlinePicture");
    }

    [TestCaseSource(nameof(LookupEndpoints))]
    public async Task LookupEndpoints_WithoutToken_ReturnUnauthorized(string method, string path)
    {
        HttpResponseMessage response = await SendAsync(_anonymous, method, path);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [TestCaseSource(nameof(LookupEndpoints))]
    public async Task LookupEndpoints_AsListener_ReturnForbiddenWithoutAnyRequest(string method, string path)
    {
        await SwitchAsync("""{ "onlineLookupEnabled": true }""");

        HttpResponseMessage response = await SendAsync(_listener, method, path);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
        Web.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task LookupEndpoints_OfANewInstallation_AreOffAndSendNoRequestAtAll()
    {
        ArtistDto artist = await ArtistAsync("Lookup Band");
        string path = $"/api/v1/artists/{artist.Id}";
        await SwitchAsync($$"""{ "fanartTvApiKey": "{{ApiKey}}" }""");

        HttpResponseMessage[] responses =
        [
            await _admin.GetAsync($"{path}/musicbrainz-matches"),
            await _admin.GetAsync($"{path}/online-picture-candidates?musicBrainzArtistId={_musicBrainzId}"),
            await _admin.GetAsync($"{path}/online-picture-candidates/{new string('a', 32)}/preview"),
            await _admin.SendJsonAsync("PUT", $"{path}/online-picture", $$"""{ "candidateId": "{{new string('a', 32)}}" }"""),
        ];

        foreach (HttpResponseMessage response in responses)
        {
            await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "artist.picture_lookup_disabled");
        }

        Web.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task ListArtistMusicBrainzMatches_On_SearchesByNameWithTheUserAgentOfTheProject()
    {
        ArtistDto artist = await ArtistAsync("Lookup Band");
        await SwitchAsync("""{ "onlineLookupEnabled": true }""");

        PageDto<MusicBrainzArtistMatchDto> page = await _admin.GetJsonAsync<PageDto<MusicBrainzArtistMatchDto>>(
            $"/api/v1/artists/{artist.Id}/musicbrainz-matches?limit=5");

        page.Should().BeEquivalentTo(new PageDto<MusicBrainzArtistMatchDto>
        {
            Items =
            [
                new MusicBrainzArtistMatchDto
                {
                    MusicBrainzArtistId = _musicBrainzId,
                    Name = "Lookup Band",
                    SortName = "Lookup Band",
                    Disambiguation = "simulated band",
                    Type = "Group",
                    Country = "GB",
                    LifeSpanBegin = "1999",
                    Score = 100,
                    IsRemembered = false,
                },
            ],
            Offset = 0,
            Limit = 5,
            Total = 1,
        });
        SimulatedRequest request = Web.Requests.Should().ContainSingle().Subject;
        request.Url.AbsoluteUri.Should().StartWith(MusicBrainz + "?query=artist%3A%22Lookup%20Band%22&fmt=json&limit=5");
        request.Headers["User-Agent"].Should().MatchRegex(@"^WaveMP/\S+ \( https://github\.com/wavemp-app/wavemp \)$");
        request.Headers["User-Agent"].Should().NotContain("@");
    }

    [Test]
    public async Task ListArtistMusicBrainzMatches_MusicBrainzFails_IsBadGateway()
    {
        ArtistDto artist = await ArtistAsync("Silent Band");
        await SwitchAsync("""{ "onlineLookupEnabled": true }""");

        HttpResponseMessage response = await _admin.GetAsync($"/api/v1/artists/{artist.Id}/musicbrainz-matches");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadGateway, "artist.picture_lookup_unavailable");
    }

    [Test]
    public async Task ListArtistPictureCandidates_WithoutMusicBrainzArtist_IsValidationProblem()
    {
        ArtistDto artist = await ArtistAsync("Silent Band");
        await SwitchAsync("""{ "onlineLookupEnabled": true }""");

        HttpResponseMessage response = await _admin.GetAsync($"/api/v1/artists/{artist.Id}/online-picture-candidates");

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle(error => error.Field == "musicBrainzArtistId");
        Web.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task OnlineLookup_TakeOverOfAWikimediaPicture_ShowsItWithItsPlainTextCreditAndRemembersTheArtist()
    {
        ArtistDto artist = await ArtistAsync("Lookup Band");
        string path = $"/api/v1/artists/{artist.Id}";
        await SwitchAsync($$"""{ "onlineLookupEnabled": true, "fanartTvApiKey": "{{ApiKey}}" }""");

        HttpResponseMessage listed = await _admin.GetAsync(
            $"{path}/online-picture-candidates?musicBrainzArtistId={_musicBrainzId}");
        string listedJson = await listed.Content.ReadAsStringAsync();
        listed.StatusCode.Should().Be(HttpStatusCode.OK, listedJson);
        PageDto<ArtistPictureCandidateDto> candidates =
            (await listed.Content.ReadFromJsonAsync<PageDto<ArtistPictureCandidateDto>>(
                HttpClientExtensions.JsonOptions))!;
        candidates.Items.Select(candidate => candidate.Provider).Should().Equal(
            ArtistPictureProviderDto.Wikimedia,
            ArtistPictureProviderDto.FanartTv);
        ArtistPictureCandidateDto wikimedia = candidates.Items[0];
        wikimedia.Should().BeEquivalentTo(new ArtistPictureCandidateDto
        {
            Id = wikimedia.Id,
            Provider = ArtistPictureProviderDto.Wikimedia,
            MusicBrainzArtistId = _musicBrainzId,
            Width = 600,
            Height = 400,
            Author = "Jane Doe",
            License = "CC BY-SA 4.0",
            LicenseUrl = null,
            SourceUrl = "https://commons.wikimedia.org/wiki/File:Band.png",
        });
        listedJson.Should().NotContain("<script").And.NotContain("javascript:").And.NotContain(ApiKey);

        HttpResponseMessage preview = await _admin.GetAsync(
            $"{path}/online-picture-candidates/{wikimedia.Id}/preview?size=300");
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");

        HttpResponseMessage takenOver = await _admin.SendJsonAsync(
            "PUT",
            $"{path}/online-picture",
            $$"""{ "candidateId": "{{wikimedia.Id}}", "crop": { "x": 0.25, "y": 0, "size": 1 } }""");
        string takenOverJson = await takenOver.Content.ReadAsStringAsync();
        takenOver.StatusCode.Should().Be(HttpStatusCode.OK, takenOverJson);
        ArtistDto updated = (await takenOver.Content.ReadFromJsonAsync<ArtistDto>(HttpClientExtensions.JsonOptions))!;

        updated.Picture.Should().BeEquivalentTo(new
        {
            Kind = ArtistPictureKindDto.Online,
            HasLocalPicture = false,
            HasUploadedPicture = false,
            HasOnlinePicture = true,
            OnlineCredit = new ArtistPictureCreditDto
            {
                Provider = ArtistPictureProviderDto.Wikimedia,
                Author = "Jane Doe",
                License = "CC BY-SA 4.0",
                LicenseUrl = null,
                SourceUrl = "https://commons.wikimedia.org/wiki/File:Band.png",
            },
            MusicBrainzArtistId = _musicBrainzId,
        });
        updated.Picture.Version.Should().StartWith("o-");
        HttpResponseMessage shown = await _listener.GetAsync($"{path}/picture?size=1000");
        shown.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ImageSizeAsync(await shown.Content.ReadAsByteArrayAsync())).Should().Be((400, 400));

        // The full picture was asked in the standard width 1280 and followed one redirect inside Wikimedia.
        Web.Requests.Select(request => request.Url.AbsoluteUri).Should().Contain([FullUrl, FullTarget]);
        Web.Requests.Should().Contain(request => request.Url.AbsoluteUri.Contains("iiurlwidth=1280"));

        // The key only went to fanart.tv in its header; it is in no address, answer, log entry or cache file.
        Web.Requests.Where(request => request.Headers.ContainsKey("api-key")).Should().ContainSingle()
            .Which.Url.Host.Should().Be("webservice.fanart.tv");
        Web.Requests.Should().AllSatisfy(request => request.Url.AbsoluteUri.Should().NotContain(ApiKey));
        takenOverJson.Should().NotContain(ApiKey);
        _factory.Log.Entries.Should().NotContain(entry => entry.Contains(ApiKey));
        foreach (string file in Directory.EnumerateFiles(
                     Path.Combine(_factory.DataPath, "cache"),
                     "*",
                     SearchOption.AllDirectories))
        {
            Path.GetFileName(file).Should().NotContain(ApiKey);
            if (file.EndsWith(".json", StringComparison.Ordinal))
            {
                (await File.ReadAllTextAsync(file)).Should().NotContain(ApiKey);
            }
        }

        // The next lookup uses the remembered MusicBrainz artist and the cached answers.
        int sent = Web.Requests.Count;
        PageDto<ArtistPictureCandidateDto> again =
            await _admin.GetJsonAsync<PageDto<ArtistPictureCandidateDto>>($"{path}/online-picture-candidates");
        again.Items.Should().HaveCount(2);
        Web.Requests.Should().HaveCount(sent);
        CacheUsageDto cache = await _admin.GetJsonAsync<CacheUsageDto>("/api/v1/cache");
        cache.ArtistLookups.EntryCount.Should().BePositive();
    }

    [Test]
    public async Task TakeOverArtistOnlinePicture_RedirectToAForeignHost_IsRefusedWithoutFollowingIt()
    {
        ArtistDto artist = await ArtistAsync("Fanart Band");
        string path = $"/api/v1/artists/{artist.Id}";
        await SwitchAsync($$"""{ "onlineLookupEnabled": true, "fanartTvApiKey": "{{ApiKey}}" }""");
        PageDto<ArtistPictureCandidateDto> candidates = await _admin.GetJsonAsync<PageDto<ArtistPictureCandidateDto>>(
            $"{path}/online-picture-candidates?musicBrainzArtistId={_musicBrainzId}");
        ArtistPictureCandidateDto trap = candidates.Items.Single(candidate =>
            candidate.Provider == ArtistPictureProviderDto.FanartTv);
        Web.Reset();
        Web.On(
            url => url.AbsoluteUri == FanartThumb,
            _ => Redirect("http://169.254.169.254/latest/meta-data"));

        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PUT",
            $"{path}/online-picture",
            $$"""{ "candidateId": "{{trap.Id}}" }""");

        await response.ShouldBeProblemAsync(HttpStatusCode.BadGateway, "artist.picture_lookup_unavailable");
        Web.Requests.Select(request => request.Url.Host).Should().Equal("assets.fanart.tv");
        (await _admin.GetJsonAsync<ArtistDto>(path)).Picture.HasOnlinePicture.Should().BeFalse();
    }

    [Test]
    public async Task GetArtistPictureCandidatePreview_UnknownOrForeignCandidate_IsNotFound()
    {
        ArtistDto artist = await ArtistAsync("Lookup Band");
        ArtistDto other = await ArtistAsync("Silent Band");
        await SwitchAsync("""{ "onlineLookupEnabled": true }""");
        PageDto<ArtistPictureCandidateDto> candidates = await _admin.GetJsonAsync<PageDto<ArtistPictureCandidateDto>>(
            $"/api/v1/artists/{artist.Id}/online-picture-candidates?musicBrainzArtistId={_musicBrainzId}");

        HttpResponseMessage unknown = await _admin.GetAsync(
            $"/api/v1/artists/{artist.Id}/online-picture-candidates/{new string('b', 32)}/preview");
        HttpResponseMessage foreign = await _admin.GetAsync(
            $"/api/v1/artists/{other.Id}/online-picture-candidates/{candidates.Items[0].Id}/preview");

        await unknown.ShouldBeProblemAsync(HttpStatusCode.NotFound, "artist.picture_candidate_not_found");
        await foreign.ShouldBeProblemAsync(HttpStatusCode.NotFound, "artist.picture_candidate_not_found");
    }

    [TestCase("""{ "candidateId": "../../etc/passwd" }""", "candidateId")]
    [TestCase("""{ }""", "candidateId")]
    [TestCase("""{ "candidateId": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "crop": { "x": 1.5, "y": 0, "size": 1 } }""", "crop.x")]
    [TestCase("""{ "candidateId": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "crop": { "x": 0, "y": 0, "size": 0 } }""", "crop.size")]
    public async Task TakeOverArtistOnlinePicture_InvalidRequest_IsValidationProblemWithoutRequest(
        string body,
        string field)
    {
        ArtistDto artist = await ArtistAsync("Lookup Band");
        await SwitchAsync("""{ "onlineLookupEnabled": true }""");

        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PUT",
            $"/api/v1/artists/{artist.Id}/online-picture",
            body);

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().Contain(error => error.Field == field);
        Web.Requests.Should().BeEmpty();
    }

    private void GivenServices()
    {
        string search = $$"""
            {
              "count": 1, "offset": 0,
              "artists": [ { "id": "{{_musicBrainzId}}", "type": "Group", "score": 100, "name": "Lookup Band",
                "sort-name": "Lookup Band", "country": "GB", "disambiguation": "simulated band",
                "life-span": { "begin": "1999", "ended": false } } ]
            }
            """;
        Web.OnJson(MusicBrainz + "?query=", search)
            .On(
                url => url.AbsoluteUri.StartsWith(MusicBrainz + "?query=", StringComparison.Ordinal)
                    && url.AbsoluteUri.Contains("Silent", StringComparison.Ordinal),
                _ => new HttpResponseMessage(HttpStatusCode.InternalServerError))
            .OnJson($"{MusicBrainz}/{_musicBrainzId}?inc=url-rels", """
                { "relations": [ { "type": "wikidata", "url": { "resource": "https://www.wikidata.org/wiki/Q4242" } } ] }
                """)
            .OnJson(Wikidata, """
                { "entities": { "Q4242": { "claims": { "P18": [ { "rank": "normal",
                  "mainsnak": { "snaktype": "value", "datavalue": { "value": "Band.png", "type": "string" } } } ] } } } }
                """)
            .OnJson(Commons, ImageInfo(PreviewUrl))
            .On(
                url => url.AbsoluteUri.StartsWith(Commons, StringComparison.Ordinal)
                    && url.AbsoluteUri.Contains("iiurlwidth=1280", StringComparison.Ordinal),
                _ => Json(ImageInfo(FullUrl)))
            .On(PreviewUrl, _png, "image/png")
            .On(url => url.AbsoluteUri == FullUrl, _ => Redirect(FullTarget))
            .On(FullTarget, _png, "image/png")
            .OnJson($"https://webservice.fanart.tv/v3/music/{_musicBrainzId}", $$"""
                { "name": "Lookup Band", "artistthumb": [
                  { "id": "1", "url": "{{FanartThumb}}", "likes": "3" },
                  { "id": "2", "url": "javascript:alert(1)", "likes": "9" } ] }
                """)
            .On(FanartThumb, _png, "image/png")
            .On(url => url.AbsoluteUri == FanartTrap, _ => Redirect("https://evil.example/x.png"));
    }

    private static string ImageInfo(string thumbnail)
    {
        return $$"""
            {
              "query": { "pages": [ { "title": "File:Band.png", "imageinfo": [ {
                "thumburl": "{{thumbnail}}",
                "url": "{{FullTarget}}",
                "descriptionurl": "https://commons.wikimedia.org/wiki/File:Band.png",
                "width": 600, "height": 400, "mime": "image/png",
                "extmetadata": {
                  "Artist": { "value": "<a href=\"//commons.wikimedia.org/wiki/User:Jane\">Jane <b>Doe</b></a><script>alert(1)</script>" },
                  "LicenseShortName": { "value": "CC BY-SA 4.0" },
                  "LicenseUrl": { "value": "javascript:alert(document.cookie)" }
                } } ] } ] }
            }
            """;
    }

    private static HttpResponseMessage Json(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    private static HttpResponseMessage Redirect(string target)
    {
        HttpResponseMessage response = new(HttpStatusCode.Found);
        response.Headers.Location = new Uri(target);
        return response;
    }

    private async Task SwitchAsync(string body)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync("PATCH", SettingsPath, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string path)
    {
        HttpRequestMessage request = new(new HttpMethod(method), path)
        {
            Content = method == "PUT"
                ? JsonContent.Create(new { candidateId = new string('a', 32) })
                : null,
        };
        return client.SendAsync(request);
    }

    private async Task<ArtistDto> ArtistAsync(string name)
    {
        PageDto<ArtistDto> artists = await _admin.GetJsonAsync<PageDto<ArtistDto>>("/api/v1/artists?limit=100");
        return artists.Items.Single(artist => artist.Name == name);
    }

    private static async Task<(int Width, int Height)> ImageSizeAsync(byte[] picture)
    {
        using TemporaryDirectory folder = new();
        string path = folder.Combine("picture.webp");
        await File.WriteAllBytesAsync(path, picture);
        return await ImageProbe.GetSizeAsync(path);
    }

    private Task AudioAsync(string path, string artist)
    {
        return TestMediaGenerator.CreateAudioAsync(
            _media.Combine(path),
            AudioFormat.Mp3,
            new TestTags
            {
                Artist = artist,
                Album = "Demo",
                Title = artist + " " + 1.ToString(CultureInfo.InvariantCulture),
            });
    }
}
