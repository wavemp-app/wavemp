using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// The Subsonic API under <c>/rest</c> and the Subsonic access of the profiles, with a small library of synthetic
/// media: sign-in with every method, the endpoints in XML and JSON, streams, downloads and covers.
/// </summary>
[TestFixture]
[Category("Integration")]
public class SubsonicApiTests
{
    private const string Access = "/api/v1/me/subsonic";

    private static readonly XNamespace _ns = "http://subsonic.org/restapi";

    private TemporaryDirectory _media = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private IReadOnlyList<TrackDto> _tracks = null!;
    private SubsonicProfile _profile = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Alpha/One/First.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = "First", Artist = "Alpha", Album = "One", Genre = "Rock", Date = "2001", Track = "1" },
            durationSeconds: 3);
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Alpha/One/Second.flac"),
            AudioFormat.Flac,
            new TestTags { Title = "Second", Artist = "Alpha", Album = "One", Genre = "Rock", Date = "2001", Track = "2" },
            durationSeconds: 3);
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Alpha/One/cover.jpg"), 64, 64);
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("The Beta/Two/Third.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = "Third", Artist = "The Beta", Album = "Two", Genre = "Pop", Date = "2005" });

        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_subsonic",
            freshDatabase: true,
            mediaPath: _media.Path);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();

        HttpResponseMessage response = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        ScanDto scan = await _admin.WaitForScanAsync(queued.Id);
        scan.Status.Should().Be(JobRunStatusDto.Succeeded);

        _tracks = (await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100")).Items;
        _tracks.Should().HaveCount(3);
        _profile = await CreateProfileAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _profile?.Client.Dispose();
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
        _media.Dispose();
    }

    [Test]
    public async Task SubsonicAccess_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage read = await _anonymous.GetAsync(Access);
        HttpResponseMessage created = await _anonymous.PostAsync($"{Access}/password", null);

        await read.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await created.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task SubsonicAccess_NewProfile_IsOffWithoutPassword()
    {
        using HttpClient client = await CreateWebClientAsync();

        SubsonicAccessDto access = await client.GetJsonAsync<SubsonicAccessDto>(Access);

        access.Enabled.Should().BeFalse();
        access.HasPassword.Should().BeFalse();
        access.PasswordCreatedAt.Should().BeNull();
    }

    [Test]
    public async Task SubsonicAccess_PasswordIsShownOnceAndCanBeRevoked()
    {
        using HttpClient client = await CreateWebClientAsync();

        HttpResponseMessage created = await client.PostAsync($"{Access}/password", null);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        created.Headers.Location!.AbsolutePath.Should().Be(Access);
        SubsonicPasswordDto password =
            (await created.Content.ReadFromJsonAsync<SubsonicPasswordDto>(HttpClientExtensions.JsonOptions))!;
        password.Password.Should().HaveLength(24);
        password.Access.HasPassword.Should().BeTrue();
        password.Access.PasswordCreatedAt.Should().NotBeNull();

        string read = await client.GetStringAsync(Access);
        read.Should().NotContain(password.Password);
        (await client.GetJsonAsync<SubsonicAccessDto>(Access)).HasPassword.Should().BeTrue();

        HttpResponseMessage revoked = await client.DeleteAsync($"{Access}/password");
        revoked.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetJsonAsync<SubsonicAccessDto>(Access)).HasPassword.Should().BeFalse();
    }

    [Test]
    public async Task SubsonicAccess_PatchWithoutValue_KeepsTheState()
    {
        using HttpClient client = await CreateWebClientAsync();
        await client.SendJsonAsync("PATCH", Access, "{\"enabled\":true}");

        HttpResponseMessage response = await client.SendJsonAsync("PATCH", Access, "{}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<SubsonicAccessDto>(HttpClientExtensions.JsonOptions))!
            .Enabled.Should().BeTrue();
    }

    [Test]
    public async Task Ping_PlainPassword_SignsInInXml()
    {
        XElement root = await XmlAsync("ping", _profile.PasswordAuth);

        root.Name.Should().Be(_ns + "subsonic-response");
        root.Attribute("status")!.Value.Should().Be("ok");
        root.Attribute("version")!.Value.Should().Be("1.16.1");
        root.Attribute("openSubsonic")!.Value.Should().Be("true");
    }

    [Test]
    public async Task Ping_EveryMethodOfSignIn_SignsInInJson()
    {
        string encoded = "enc:" + Convert.ToHexString(Encoding.UTF8.GetBytes(_profile.Password));
        string token = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(_profile.Password + "a1b2c3")));
        string[] methods =
        [
            _profile.PasswordAuth,
            $"u={Uri.EscapeDataString(_profile.Name)}&p={encoded}",
            $"u={Uri.EscapeDataString(_profile.Name.ToUpperInvariant())}&t={token}&s=a1b2c3",
            $"apiKey={_profile.Password}",
        ];

        foreach (string method in methods)
        {
            JsonElement root = await JsonAsync("ping", method);
            root.GetProperty("status").GetString().Should().Be("ok", method);
        }
    }

    [Test]
    public async Task Ping_WithViewSuffixAndFormPost_SignsIn()
    {
        HttpResponseMessage view = await _anonymous.GetAsync($"/rest/ping.view?{_profile.PasswordAuth}&f=json");
        using FormUrlEncodedContent form = new(new Dictionary<string, string>
        {
            ["u"] = _profile.Name,
            ["p"] = _profile.Password,
            ["f"] = "json",
            ["v"] = "1.16.1",
            ["c"] = "test",
        });
        HttpResponseMessage posted = await _anonymous.PostAsync("/rest/ping", form);

        (await StatusOfAsync(view)).Should().Be("ok");
        (await StatusOfAsync(posted)).Should().Be("ok");
    }

    [Test]
    public async Task Ping_WrongCredentials_FailWithTheCodesOfTheSpecification()
    {
        (await ErrorCodeAsync("ping", $"u={Uri.EscapeDataString(_profile.Name)}&p=wrong")).Should().Be(40);
        (await ErrorCodeAsync("ping", "u=Nobody&p=wrong")).Should().Be(40);
        (await ErrorCodeAsync("ping", "apiKey=wrong")).Should().Be(44);
        (await ErrorCodeAsync("ping", $"apiKey={_profile.Password}&u=x")).Should().Be(43);
        (await ErrorCodeAsync("ping", string.Empty)).Should().Be(10);
    }

    [Test]
    public async Task Ping_AccessOffOrPasswordRevoked_FailsToSignIn()
    {
        SubsonicProfile off = await CreateProfileAsync();
        await off.Client.SendJsonAsync("PATCH", Access, "{\"enabled\":false}");
        SubsonicProfile revoked = await CreateProfileAsync();
        await revoked.Client.DeleteAsync($"{Access}/password");

        (await ErrorCodeAsync("ping", off.PasswordAuth)).Should().Be(40);
        (await ErrorCodeAsync("ping", $"apiKey={off.Password}")).Should().Be(44);
        (await ErrorCodeAsync("ping", revoked.PasswordAuth)).Should().Be(40);
    }

    [Test]
    public async Task Ping_NewPassword_ReplacesTheOldOne()
    {
        SubsonicProfile profile = await CreateProfileAsync();
        HttpResponseMessage created = await profile.Client.PostAsync($"{Access}/password", null);
        string newPassword = (await created.Content.ReadFromJsonAsync<SubsonicPasswordDto>(
            HttpClientExtensions.JsonOptions))!.Password;

        (await ErrorCodeAsync("ping", profile.PasswordAuth)).Should().Be(40);
        (await JsonAsync("ping", $"u={Uri.EscapeDataString(profile.Name)}&p={newPassword}"))
            .GetProperty("status").GetString().Should().Be("ok");
    }

    [Test]
    public async Task GetOpenSubsonicExtensions_WithoutSignIn_ListsTheExtensions()
    {
        JsonElement root = await JsonAsync("getOpenSubsonicExtensions", string.Empty);

        root.GetProperty("openSubsonicExtensions").EnumerateArray()
            .Select(extension => extension.GetProperty("name").GetString())
            .Should().Contain(["apiKeyAuthentication", "formPost", "transcodeOffset"]);
    }

    [Test]
    public async Task TokenInfo_ApiKey_NamesTheUser()
    {
        JsonElement root = await JsonAsync("tokenInfo", $"apiKey={_profile.Password}");

        root.GetProperty("tokenInfo").GetProperty("username").GetString().Should().Be(_profile.Name);
    }

    [Test]
    public async Task UnknownEndpoint_ReturnsNotFound()
    {
        HttpResponseMessage response = await _anonymous.GetAsync($"/rest/createUser?{_profile.PasswordAuth}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Preflight_FromAnotherOrigin_IsAllowed()
    {
        using HttpRequestMessage request = new(HttpMethod.Options, "/rest/ping");
        request.Headers.Add("Origin", "https://player.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        HttpResponseMessage response = await _anonymous.SendAsync(request);

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("*");
    }

    [Test]
    public async Task GetLicenseAndMusicFolders_Answer()
    {
        JsonElement license = await JsonAsync("getLicense", _profile.PasswordAuth);
        XElement folders = await XmlAsync("getMusicFolders", _profile.PasswordAuth);

        license.GetProperty("license").GetProperty("valid").GetBoolean().Should().BeTrue();
        folders.Element(_ns + "musicFolders")!.Element(_ns + "musicFolder")!.Attribute("id")!.Value.Should().Be("1");
    }

    [Test]
    public async Task GetArtists_GroupsTheArtistsWithoutArticle()
    {
        JsonElement json = (await JsonAsync("getArtists", _profile.PasswordAuth)).GetProperty("artists");
        XElement xml = (await XmlAsync("getArtists", _profile.PasswordAuth)).Element(_ns + "artists")!;

        json.GetProperty("ignoredArticles").GetString().Should().Contain("The");
        JsonElement[] indexes = [.. json.GetProperty("index").EnumerateArray()];
        indexes.Select(index => index.GetProperty("name").GetString()).Should().Equal("A", "B");
        indexes[1].GetProperty("artist")[0].GetProperty("name").GetString().Should().Be("The Beta");
        indexes[0].GetProperty("artist")[0].GetProperty("albumCount").GetInt32().Should().Be(1);
        xml.Elements(_ns + "index").Select(index => index.Attribute("name")!.Value).Should().Equal("A", "B");
    }

    [Test]
    public async Task GetIndexes_ListsTheArtists()
    {
        JsonElement indexes = (await JsonAsync("getIndexes", _profile.PasswordAuth)).GetProperty("indexes");

        indexes.GetProperty("lastModified").GetInt64().Should().BePositive();
        indexes.GetProperty("index").GetArrayLength().Should().Be(2);
    }

    [Test]
    public async Task GetArtistAlbumAndSong_ShowTheLibrary()
    {
        TrackDto first = Track("First");

        JsonElement artist = (await JsonAsync("getArtist", $"{_profile.PasswordAuth}&id={first.Artist!.Id}"))
            .GetProperty("artist");
        JsonElement album = (await JsonAsync("getAlbum", $"{_profile.PasswordAuth}&id={first.Album!.Id}"))
            .GetProperty("album");
        JsonElement song = (await JsonAsync("getSong", $"{_profile.PasswordAuth}&id={first.Id}"))
            .GetProperty("song");
        XElement xmlAlbum = (await XmlAsync("getAlbum", $"{_profile.PasswordAuth}&id={first.Album!.Id}"))
            .Element(_ns + "album")!;

        artist.GetProperty("name").GetString().Should().Be("Alpha");
        artist.GetProperty("album")[0].GetProperty("name").GetString().Should().Be("One");
        album.GetProperty("songCount").GetInt32().Should().Be(2);
        album.GetProperty("coverArt").GetString().Should().Be($"al-{first.Album!.Id}");
        album.GetProperty("song").EnumerateArray().Select(item => item.GetProperty("title").GetString())
            .Should().Equal("First", "Second");
        song.GetProperty("id").GetString().Should().Be(first.Id.ToString());
        song.GetProperty("album").GetString().Should().Be("One");
        song.GetProperty("artist").GetString().Should().Be("Alpha");
        song.GetProperty("year").GetInt32().Should().Be(2001);
        song.GetProperty("genre").GetString().Should().Be("Rock");
        song.GetProperty("suffix").GetString().Should().Be("mp3");
        song.GetProperty("contentType").GetString().Should().Be("audio/mpeg");
        song.GetProperty("duration").GetInt32().Should().BeInRange(2, 4);
        song.GetProperty("isDir").GetBoolean().Should().BeFalse();
        song.GetProperty("albumId").GetString().Should().Be(first.Album!.Id.ToString());
        xmlAlbum.Elements(_ns + "song").Should().HaveCount(2);
    }

    [Test]
    public async Task GetSong_UnknownOrMissingId_FailsWithTheCode()
    {
        (await ErrorCodeAsync("getSong", $"{_profile.PasswordAuth}&id={Guid.NewGuid()}")).Should().Be(70);
        (await ErrorCodeAsync("getSong", $"{_profile.PasswordAuth}&id=nothing")).Should().Be(70);
        (await ErrorCodeAsync("getSong", _profile.PasswordAuth)).Should().Be(10);
    }

    [Test]
    public async Task GetMusicDirectory_ArtistHoldsAlbumsAndAlbumHoldsSongs()
    {
        TrackDto first = Track("First");

        JsonElement artist = (await JsonAsync("getMusicDirectory", $"{_profile.PasswordAuth}&id={first.Artist!.Id}"))
            .GetProperty("directory");
        JsonElement album = (await JsonAsync("getMusicDirectory", $"{_profile.PasswordAuth}&id={first.Album!.Id}"))
            .GetProperty("directory");

        artist.GetProperty("name").GetString().Should().Be("Alpha");
        JsonElement folder = artist.GetProperty("child")[0];
        folder.GetProperty("isDir").GetBoolean().Should().BeTrue();
        folder.GetProperty("id").GetString().Should().Be(first.Album!.Id.ToString());
        album.GetProperty("child").GetArrayLength().Should().Be(2);
        album.GetProperty("child")[0].GetProperty("isDir").GetBoolean().Should().BeFalse();
    }

    [TestCase("alphabeticalByName", "One,Two")]
    [TestCase("alphabeticalByArtist", "One,Two")]
    [TestCase("byGenre&genre=Pop", "Two")]
    [TestCase("byYear&fromYear=2000&toYear=2002", "One")]
    public async Task GetAlbumList2_ListsTheAlbums(string type, string expected)
    {
        JsonElement list = (await JsonAsync("getAlbumList2", $"{_profile.PasswordAuth}&type={type}&size=10"))
            .GetProperty("albumList2");

        list.GetProperty("album").EnumerateArray().Select(album => album.GetProperty("name").GetString())
            .Should().Equal(expected.Split(','));
    }

    [Test]
    public async Task GetAlbumList2_RandomAndNewest_ListEveryAlbum()
    {
        JsonElement random = (await JsonAsync("getAlbumList2", $"{_profile.PasswordAuth}&type=random"))
            .GetProperty("albumList2");
        XElement newest = (await XmlAsync("getAlbumList2", $"{_profile.PasswordAuth}&type=newest"))
            .Element(_ns + "albumList2")!;

        random.GetProperty("album").GetArrayLength().Should().Be(2);
        newest.Elements(_ns + "album").Should().HaveCount(2);
    }

    [Test]
    public async Task GetAlbumList2_WithoutType_FailsWithMissingParameter()
    {
        (await ErrorCodeAsync("getAlbumList2", _profile.PasswordAuth)).Should().Be(10);
    }

    [Test]
    public async Task GetRandomSongs_ListsSongs()
    {
        JsonElement songs = (await JsonAsync("getRandomSongs", $"{_profile.PasswordAuth}&size=2"))
            .GetProperty("randomSongs");
        JsonElement pop = (await JsonAsync("getRandomSongs", $"{_profile.PasswordAuth}&genre=Pop"))
            .GetProperty("randomSongs");

        songs.GetProperty("song").GetArrayLength().Should().Be(2);
        pop.GetProperty("song").EnumerateArray().Select(song => song.GetProperty("title").GetString())
            .Should().Equal("Third");
    }

    [Test]
    public async Task Search3_FindsArtistsAlbumsAndSongs()
    {
        JsonElement result = (await JsonAsync("search3", $"{_profile.PasswordAuth}&query=Third"))
            .GetProperty("searchResult3");
        JsonElement all = (await JsonAsync("search3", $"{_profile.PasswordAuth}&query=%22%22&songCount=100"))
            .GetProperty("searchResult3");
        XElement xml = (await XmlAsync("search3", $"{_profile.PasswordAuth}&query=Alpha"))
            .Element(_ns + "searchResult3")!;

        result.GetProperty("song").EnumerateArray().Select(song => song.GetProperty("title").GetString())
            .Should().Equal("Third");
        all.GetProperty("song").GetArrayLength().Should().Be(3);
        all.GetProperty("artist").GetArrayLength().Should().Be(2);
        xml.Elements(_ns + "artist").Select(artist => artist.Attribute("name")!.Value).Should().Equal("Alpha");
    }

    [Test]
    public async Task StarAndRating_AreTheFavoritesAndRatingsOfTheProfile()
    {
        SubsonicProfile profile = await CreateProfileAsync();
        TrackDto first = Track("First");

        (await JsonAsync("star", $"{profile.PasswordAuth}&id={first.Id}&albumId={first.Album!.Id}"))
            .GetProperty("status").GetString().Should().Be("ok");
        (await JsonAsync("star", $"{profile.PasswordAuth}&artistId={first.Artist!.Id}"))
            .GetProperty("status").GetString().Should().Be("ok");
        (await JsonAsync("setRating", $"{profile.PasswordAuth}&id={first.Id}&rating=4"))
            .GetProperty("status").GetString().Should().Be("ok");

        JsonElement starred = (await JsonAsync("getStarred2", profile.PasswordAuth)).GetProperty("starred2");
        starred.GetProperty("song")[0].GetProperty("id").GetString().Should().Be(first.Id.ToString());
        starred.GetProperty("song")[0].GetProperty("starred").GetString().Should().NotBeNullOrEmpty();
        starred.GetProperty("song")[0].GetProperty("userRating").GetInt32().Should().Be(4);
        starred.GetProperty("album")[0].GetProperty("name").GetString().Should().Be("One");
        starred.GetProperty("artist")[0].GetProperty("name").GetString().Should().Be("Alpha");
        TrackDto web = await profile.Client.GetJsonAsync<TrackDto>($"/api/v1/tracks/{first.Id}");
        web.IsFavorite.Should().BeTrue();
        web.Rating.Should().Be(4);
        JsonElement starredAlbums = (await JsonAsync("getAlbumList2", $"{profile.PasswordAuth}&type=starred"))
            .GetProperty("albumList2");
        starredAlbums.GetProperty("album").GetArrayLength().Should().Be(1);

        await JsonAsync("unstar", $"{profile.PasswordAuth}&id={first.Id}&albumId={first.Album!.Id}");
        JsonElement after = (await JsonAsync("getStarred2", profile.PasswordAuth)).GetProperty("starred2");
        after.GetProperty("song").GetArrayLength().Should().Be(0);
        after.GetProperty("album").GetArrayLength().Should().Be(0);
        after.GetProperty("artist").GetArrayLength().Should().Be(1);
    }

    [Test]
    public async Task StarAndRating_UnknownSong_FailWithNotFound()
    {
        (await ErrorCodeAsync("star", $"{_profile.PasswordAuth}&id={Guid.NewGuid()}")).Should().Be(70);
        (await ErrorCodeAsync("setRating", $"{_profile.PasswordAuth}&id={Guid.NewGuid()}&rating=3")).Should().Be(70);
    }

    [Test]
    public async Task Scrobble_Submission_RecordsThePlayInTheHistory()
    {
        SubsonicProfile profile = await CreateProfileAsync();
        TrackDto third = Track("Third");
        long time = DateTimeOffset.UtcNow.AddMinutes(-2).ToUnixTimeMilliseconds();

        JsonElement root = await JsonAsync("scrobble", $"{profile.PasswordAuth}&id={third.Id}&time={time}");

        root.GetProperty("status").GetString().Should().Be("ok");
        PageDto<PlayHistoryEntryDto> history =
            await profile.Client.GetJsonAsync<PageDto<PlayHistoryEntryDto>>("/api/v1/me/history");
        history.Items.Should().ContainSingle().Which.Track.Id.Should().Be(third.Id);
        JsonElement song = (await JsonAsync("getSong", $"{profile.PasswordAuth}&id={third.Id}")).GetProperty("song");
        song.GetProperty("playCount").GetInt64().Should().Be(1);
        song.GetProperty("played").GetString().Should().NotBeNullOrEmpty();
    }

    [Test]
    public async Task Scrobble_NowPlaying_ShowsInGetNowPlaying()
    {
        SubsonicProfile profile = await CreateProfileAsync();
        TrackDto second = Track("Second");

        await JsonAsync("scrobble", $"{profile.PasswordAuth}&id={second.Id}&submission=false&c=TestPlayer");
        JsonElement nowPlaying = (await JsonAsync("getNowPlaying", _profile.PasswordAuth)).GetProperty("nowPlaying");

        JsonElement entry = nowPlaying.GetProperty("entry").EnumerateArray()
            .Single(item => item.GetProperty("username").GetString() == profile.Name);
        entry.GetProperty("id").GetString().Should().Be(second.Id.ToString());
        entry.GetProperty("playerName").GetString().Should().Be("TestPlayer");
        entry.GetProperty("minutesAgo").GetInt32().Should().Be(0);
        PageDto<PlayHistoryEntryDto> history =
            await profile.Client.GetJsonAsync<PageDto<PlayHistoryEntryDto>>("/api/v1/me/history");
        history.Total.Should().Be(0);
    }

    [Test]
    public async Task NowPlayingOfTheWebApp_ShowsInGetNowPlaying()
    {
        SubsonicProfile profile = await CreateProfileAsync();
        TrackDto first = Track("First");

        HttpResponseMessage reported = await profile.Client.SendJsonAsync(
            "PUT",
            "/api/v1/me/now-playing",
            $"{{\"trackId\":\"{first.Id}\"}}");
        XElement nowPlaying = (await XmlAsync("getNowPlaying", profile.PasswordAuth)).Element(_ns + "nowPlaying")!;

        reported.StatusCode.Should().Be(HttpStatusCode.NoContent);
        nowPlaying.Elements(_ns + "entry")
            .Should().Contain(entry => entry.Attribute("username")!.Value == profile.Name
                && entry.Attribute("id")!.Value == first.Id.ToString());
    }

    [Test]
    public async Task Playlists_CanBeCreatedChangedAndDeleted()
    {
        SubsonicProfile profile = await CreateProfileAsync();
        TrackDto first = Track("First");
        TrackDto second = Track("Second");
        TrackDto third = Track("Third");

        JsonElement created = (await JsonAsync(
                "createPlaylist",
                $"{profile.PasswordAuth}&name=Road&songId={first.Id}&songId={second.Id}"))
            .GetProperty("playlist");
        string id = created.GetProperty("id").GetString()!;
        created.GetProperty("name").GetString().Should().Be("Road");
        created.GetProperty("owner").GetString().Should().Be(profile.Name);
        created.GetProperty("songCount").GetInt32().Should().Be(2);
        created.GetProperty("entry").EnumerateArray().Select(entry => entry.GetProperty("title").GetString())
            .Should().Equal("First", "Second");

        (await JsonAsync(
                "updatePlaylist",
                $"{profile.PasswordAuth}&playlistId={id}&name=Trip&songIndexToRemove=0&songIdToAdd={third.Id}"))
            .GetProperty("status").GetString().Should().Be("ok");
        JsonElement updated = (await JsonAsync("getPlaylist", $"{profile.PasswordAuth}&id={id}"))
            .GetProperty("playlist");
        updated.GetProperty("name").GetString().Should().Be("Trip");
        updated.GetProperty("entry").EnumerateArray().Select(entry => entry.GetProperty("title").GetString())
            .Should().Equal("Second", "Third");

        JsonElement replaced = (await JsonAsync(
                "createPlaylist",
                $"{profile.PasswordAuth}&playlistId={id}&songId={first.Id}"))
            .GetProperty("playlist");
        replaced.GetProperty("entry").EnumerateArray().Select(entry => entry.GetProperty("title").GetString())
            .Should().Equal("First");

        XElement playlists = (await XmlAsync("getPlaylists", profile.PasswordAuth)).Element(_ns + "playlists")!;
        playlists.Elements(_ns + "playlist").Select(playlist => playlist.Attribute("id")!.Value).Should().Equal(id);
        (await ErrorCodeAsync("getPlaylist", $"{_profile.PasswordAuth}&id={id}")).Should().Be(70);
        (await ErrorCodeAsync("getPlaylists", $"{profile.PasswordAuth}&username=someone")).Should().Be(50);

        (await JsonAsync("deletePlaylist", $"{profile.PasswordAuth}&id={id}"))
            .GetProperty("status").GetString().Should().Be("ok");
        (await ErrorCodeAsync("getPlaylist", $"{profile.PasswordAuth}&id={id}")).Should().Be(70);
    }

    [Test]
    public async Task SmartPlaylists_AreListedAsReadOnlyPlaylistsWithTheirSongsOfRightNow()
    {
        SubsonicProfile profile = await CreateProfileAsync();
        string normal = (await JsonAsync("createPlaylist", $"{profile.PasswordAuth}&name=Road&songId={Track("Third").Id}"))
            .GetProperty("playlist").GetProperty("id").GetString()!;
        HttpResponseMessage response = await profile.Client.PostAsJsonAsync("/api/v1/me/smart-playlists", new
        {
            name = "Rock",
            rules = new[] { new { field = "Genre", @operator = "Is", text = "rock" } },
            sort = "Title",
            direction = "Desc",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        SmartPlaylistDto smart =
            (await response.Content.ReadFromJsonAsync<SmartPlaylistDto>(HttpClientExtensions.JsonOptions))!;
        string id = $"sp-{smart.Id}";

        XElement playlists = (await XmlAsync("getPlaylists", profile.PasswordAuth)).Element(_ns + "playlists")!;
        playlists.Elements(_ns + "playlist").Select(playlist => playlist.Attribute("id")!.Value)
            .Should().Equal(normal, id);
        XElement listed = playlists.Elements(_ns + "playlist").Last();
        listed.Attribute("name")!.Value.Should().Be("Rock");
        listed.Attribute("readonly")!.Value.Should().Be("true");
        listed.Attribute("songCount")!.Value.Should().Be("2");
        listed.Attribute("coverArt")!.Value.Should().Be(id);
        playlists.Elements(_ns + "playlist").First().Attribute("readonly")!.Value.Should().Be("false");

        // The songs are those of the smart playlist in the REST API, in its order.
        JsonElement playlist = (await JsonAsync("getPlaylist", $"{profile.PasswordAuth}&id={id}")).GetProperty("playlist");
        PageDto<TrackDto> tracks =
            await profile.Client.GetJsonAsync<PageDto<TrackDto>>($"/api/v1/me/smart-playlists/{smart.Id}/tracks");
        playlist.GetProperty("id").GetString().Should().Be(id);
        playlist.GetProperty("readonly").GetBoolean().Should().BeTrue();
        playlist.GetProperty("owner").GetString().Should().Be(profile.Name);
        playlist.GetProperty("entry").EnumerateArray().Select(entry => entry.GetProperty("id").GetString())
            .Should().Equal(tracks.Items.Select(track => track.Id.ToString()));
        playlist.GetProperty("entry").EnumerateArray().Select(entry => entry.GetProperty("title").GetString())
            .Should().Equal("Second", "First");

        HttpResponseMessage cover = await _anonymous.GetAsync($"/rest/getCoverArt?{profile.PasswordAuth}&id={id}");
        cover.StatusCode.Should().Be(HttpStatusCode.OK);
        cover.Content.Headers.ContentType!.MediaType.Should().StartWith("image/");

        // Other profiles do not see it, and unknown or invalid ids are not found.
        (await ErrorCodeAsync("getPlaylist", $"{_profile.PasswordAuth}&id={id}")).Should().Be(70);
        (await ErrorCodeAsync("getPlaylist", $"{profile.PasswordAuth}&id=sp-{Guid.NewGuid()}")).Should().Be(70);
        (await ErrorCodeAsync("getPlaylist", $"{profile.PasswordAuth}&id=sp-nothing")).Should().Be(70);
        (await ErrorCodeAsync("getCoverArt", $"{_profile.PasswordAuth}&id={id}")).Should().Be(70);
    }

    [Test]
    public async Task SmartPlaylists_CannotBeChangedButCanBeDeleted()
    {
        SubsonicProfile profile = await CreateProfileAsync();
        HttpResponseMessage response = await profile.Client.PostAsJsonAsync(
            "/api/v1/me/smart-playlists",
            new { name = "Everything", rules = Array.Empty<object>() });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        SmartPlaylistDto smart =
            (await response.Content.ReadFromJsonAsync<SmartPlaylistDto>(HttpClientExtensions.JsonOptions))!;
        string id = $"sp-{smart.Id}";
        TrackDto third = Track("Third");

        (await ErrorCodeAsync("updatePlaylist", $"{profile.PasswordAuth}&playlistId={id}&name=Other"))
            .Should().Be(50);
        (await ErrorCodeAsync("updatePlaylist", $"{profile.PasswordAuth}&playlistId={id}&songIdToAdd={third.Id}"))
            .Should().Be(50);
        (await ErrorCodeAsync("createPlaylist", $"{profile.PasswordAuth}&playlistId={id}&songId={third.Id}"))
            .Should().Be(50);
        (await ErrorCodeAsync("updatePlaylist", $"{profile.PasswordAuth}&playlistId=sp-{Guid.NewGuid()}&name=Other"))
            .Should().Be(70);
        (await ErrorCodeAsync("updatePlaylist", $"{_profile.PasswordAuth}&playlistId={id}&name=Other"))
            .Should().Be(70);
        (await ErrorCodeAsync("deletePlaylist", $"{_profile.PasswordAuth}&id={id}")).Should().Be(70);
        SmartPlaylistDto unchanged =
            await profile.Client.GetJsonAsync<SmartPlaylistDto>($"/api/v1/me/smart-playlists/{smart.Id}");
        unchanged.Name.Should().Be("Everything");

        (await JsonAsync("deletePlaylist", $"{profile.PasswordAuth}&id={id}"))
            .GetProperty("status").GetString().Should().Be("ok");
        (await profile.Client.GetAsync($"/api/v1/me/smart-playlists/{smart.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCodeAsync("getPlaylist", $"{profile.PasswordAuth}&id={id}")).Should().Be(70);
        (await ErrorCodeAsync("deletePlaylist", $"{profile.PasswordAuth}&id={id}")).Should().Be(70);
        (await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100")).Items.Should().HaveCount(3);
    }

    [Test]
    public async Task Stream_CommonFormat_DeliversTheFileWithRanges()
    {
        TrackDto first = Track("First");
        byte[] file = await File.ReadAllBytesAsync(_media.Combine("Alpha/One/First.mp3"));

        HttpResponseMessage response = await _anonymous.GetAsync($"/rest/stream?{_profile.PasswordAuth}&id={first.Id}");
        using HttpRequestMessage range = new(HttpMethod.Get, $"/rest/stream?{_profile.PasswordAuth}&id={first.Id}");
        range.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 9);
        HttpResponseMessage partial = await _anonymous.SendAsync(range);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("audio/mpeg");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(file);
        partial.StatusCode.Should().Be(HttpStatusCode.PartialContent);
        (await partial.Content.ReadAsByteArrayAsync()).Should().HaveCount(10);
    }

    [Test]
    public async Task Stream_MaxBitRateBelowTheFile_TranscodesToMp3()
    {
        TrackDto second = Track("Second");

        HttpResponseMessage response = await _anonymous.GetAsync(
            $"/rest/stream?{_profile.PasswordAuth}&id={second.Id}&maxBitRate=64");
        HttpResponseMessage opus = await _anonymous.GetAsync(
            $"/rest/stream?{_profile.PasswordAuth}&id={second.Id}&format=opus&timeOffset=1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("audio/mpeg");
        byte[] mp3 = await response.Content.ReadAsByteArrayAsync();
        mp3.Should().NotBeEmpty();
        mp3.Length.Should().BeLessThan((int)new FileInfo(_media.Combine("Alpha/One/Second.flac")).Length);
        opus.Content.Headers.ContentType!.MediaType.Should().Be("audio/ogg");
        byte[] ogg = await opus.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(ogg, 0, 4).Should().Be("OggS");
    }

    [Test]
    public async Task Stream_UnknownSong_AnswersTheErrorInTheFormat()
    {
        HttpResponseMessage response = await _anonymous.GetAsync(
            $"/rest/stream?{_profile.PasswordAuth}&id={Guid.NewGuid()}&f=json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ErrorCodeOfAsync(response)).Should().Be(70);
    }

    [Test]
    public async Task Download_OnlyWhileTheLibrarySettingsAllowIt()
    {
        TrackDto second = Track("Second");
        string path = $"/rest/download?{_profile.PasswordAuth}&id={second.Id}&f=json";

        HttpResponseMessage forbidden = await _anonymous.GetAsync(path);
        (await ErrorCodeOfAsync(forbidden)).Should().Be(50);

        HttpResponseMessage allowed = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/library",
            "{\"subsonicDownloads\":true}");
        allowed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await allowed.Content.ReadFromJsonAsync<LibrarySettingsDto>(HttpClientExtensions.JsonOptions))!
            .SubsonicDownloads.Should().BeTrue();
        try
        {
            HttpResponseMessage download = await _anonymous.GetAsync(path);

            download.StatusCode.Should().Be(HttpStatusCode.OK);
            download.Content.Headers.ContentType!.MediaType.Should().Be("audio/flac");
            download.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("Second.flac");
            (await download.Content.ReadAsByteArrayAsync())
                .Should().Equal(await File.ReadAllBytesAsync(_media.Combine("Alpha/One/Second.flac")));
        }
        finally
        {
            await _admin.SendJsonAsync("PATCH", "/api/v1/settings/library", "{\"subsonicDownloads\":false}");
        }
    }

    [Test]
    public async Task GetCoverArt_DeliversTheAlbumCover()
    {
        TrackDto first = Track("First");

        HttpResponseMessage album = await _anonymous.GetAsync(
            $"/rest/getCoverArt?{_profile.PasswordAuth}&id=al-{first.Album!.Id}");
        HttpResponseMessage song = await _anonymous.GetAsync(
            $"/rest/getCoverArt?{_profile.PasswordAuth}&id=tr-{first.Id}");
        HttpResponseMessage plain = await _anonymous.GetAsync(
            $"/rest/getCoverArt?{_profile.PasswordAuth}&id={first.Album!.Id}");
        HttpResponseMessage unknown = await _anonymous.GetAsync(
            $"/rest/getCoverArt?{_profile.PasswordAuth}&id=al-{Guid.NewGuid()}&f=json");

        album.StatusCode.Should().Be(HttpStatusCode.OK);
        album.Content.Headers.ContentType!.MediaType.Should().StartWith("image/");
        song.StatusCode.Should().Be(HttpStatusCode.OK);
        plain.Content.Headers.ContentType!.MediaType.Should().StartWith("image/");
        (await ErrorCodeOfAsync(unknown)).Should().Be(70);
    }

    private TrackDto Track(string title)
    {
        return _tracks.Single(track => track.Title == title);
    }

    private async Task<HttpClient> CreateWebClientAsync()
    {
        UserDto user = await _admin.CreateUserAsync(new { name = $"Subsonic {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        return _factory.CreateAuthorizedClient(session.Token);
    }

    /// <summary>
    /// A profile with the Subsonic access on and an app password.
    /// </summary>
    private async Task<SubsonicProfile> CreateProfileAsync()
    {
        string name = $"Subsonic {HttpClientExtensions.Unique()}";
        UserDto user = await _admin.CreateUserAsync(new { name });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        HttpClient client = _factory.CreateAuthorizedClient(session.Token);
        HttpResponseMessage enabled = await client.SendJsonAsync("PATCH", Access, "{\"enabled\":true}");
        enabled.StatusCode.Should().Be(HttpStatusCode.OK);
        HttpResponseMessage created = await client.PostAsync($"{Access}/password", null);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        SubsonicPasswordDto password =
            (await created.Content.ReadFromJsonAsync<SubsonicPasswordDto>(HttpClientExtensions.JsonOptions))!;
        return new SubsonicProfile(name, password.Password, client);
    }

    private async Task<JsonElement> JsonAsync(string endpoint, string query)
    {
        HttpResponseMessage response = await _anonymous.GetAsync($"/rest/{endpoint}?{query}&f=json&v=1.16.1&c=test");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
        return document.RootElement.GetProperty("subsonic-response").Clone();
    }

    private async Task<XElement> XmlAsync(string endpoint, string query)
    {
        HttpResponseMessage response = await _anonymous.GetAsync($"/rest/{endpoint}?{query}&v=1.16.1&c=test");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/xml");
        XElement root = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root!;
        root.Attribute("status")!.Value.Should().Be("ok", root.ToString());
        return root;
    }

    private async Task<int> ErrorCodeAsync(string endpoint, string query)
    {
        JsonElement root = await JsonAsync(endpoint, query);
        root.GetProperty("status").GetString().Should().Be("failed");

        // The same error in XML.
        HttpResponseMessage xml = await _anonymous.GetAsync($"/rest/{endpoint}?{query}");
        XElement error = XDocument.Parse(await xml.Content.ReadAsStringAsync()).Root!.Element(_ns + "error")!;
        int code = root.GetProperty("error").GetProperty("code").GetInt32();
        error.Attribute("code")!.Value.Should().Be(code.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return code;
    }

    private static async Task<int> ErrorCodeOfAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
        JsonElement root = document.RootElement.GetProperty("subsonic-response");
        root.GetProperty("status").GetString().Should().Be("failed");
        return root.GetProperty("error").GetProperty("code").GetInt32();
    }

    private static async Task<string?> StatusOfAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
        return document.RootElement.GetProperty("subsonic-response").GetProperty("status").GetString();
    }

    private sealed record SubsonicProfile(string Name, string Password, HttpClient Client)
    {
        public string PasswordAuth => $"u={Uri.EscapeDataString(Name)}&p={Password}";
    }
}
