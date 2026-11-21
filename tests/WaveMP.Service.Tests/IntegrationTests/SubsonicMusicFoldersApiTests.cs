using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using WaveMP.Core.Subsonic.Requests;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// The music folders of the Subsonic API with three media sources: the media folder of the service, a second active
/// source and a deactivated one. Every active source is a music folder, and <c>musicFolderId</c> narrows the lists to
/// the tracks of its source.
/// </summary>
[TestFixture]
[Category("Integration")]
public class SubsonicMusicFoldersApiTests
{
    private const string Access = "/api/v1/me/subsonic";

    private TemporaryDirectory _media = null!;
    private TemporaryDirectory _others = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private IReadOnlyList<TrackDto> _tracks = null!;
    private MediaSourceDto _first = null!;
    private MediaSourceDto _second = null!;
    private MediaSourceDto _archive = null!;
    private SubsonicProfile _profile = null!;

    private string SecondFolder => FolderOf(_second);

    private string ArchiveFolder => FolderOf(_archive);

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        _others = new TemporaryDirectory();
        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine("Alpha/One/First.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = "First", Artist = "Alpha", Album = "One", Genre = "Rock", Date = "2001" });
        string second = _others.CreateDirectory("Second");
        await TestMediaGenerator.CreateAudioAsync(
            Path.Combine(second, "Gamma", "Three", "Fourth.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = "Fourth", Artist = "Gamma", Album = "Three", Genre = "Rock", Date = "2001" });
        string archive = _others.CreateDirectory("Archive");
        await TestMediaGenerator.CreateAudioAsync(
            Path.Combine(archive, "Delta", "Four", "Fifth.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = "Fifth", Artist = "Delta", Album = "Four", Genre = "Rock", Date = "2001" });

        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_subsonic_folders",
            freshDatabase: true,
            mediaPath: _media.Path);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();

        HttpResponseMessage scan = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await scan.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        (await _admin.WaitForScanAsync(queued.Id)).Status.Should().Be(JobRunStatusDto.Succeeded);
        _second = await CreateSourceAsync("Second", second);
        _archive = await CreateSourceAsync("Archive", archive);
        HttpResponseMessage deactivated = await _admin.PatchAsJsonAsync(
            $"/api/v1/sources/{_archive.Id}",
            new { isActive = false });
        deactivated.StatusCode.Should().Be(HttpStatusCode.OK, await deactivated.Content.ReadAsStringAsync());
        _first = (await _admin.GetJsonAsync<PageDto<MediaSourceDto>>("/api/v1/sources")).Items
            .Single(source => source.Id == MediaSource.DefaultId);

        _tracks = (await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100")).Items;
        _tracks.Select(track => track.Title).Should().BeEquivalentTo("First", "Fourth", "Fifth");
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
        _others.Dispose();
        _media.Dispose();
    }

    [Test]
    public async Task GetMusicFolders_ListsOneFolderPerActiveSourceWithItsName()
    {
        JsonElement folders = (await JsonAsync("getMusicFolders", _profile.PasswordAuth)).GetProperty("musicFolders");

        (int Id, string? Name)[] expected = [(1, _first.Name), (SubsonicIds.MusicFolder(_second.Id), _second.Name)];
        folders.GetProperty("musicFolder").EnumerateArray()
            .Select(folder => (Id: folder.GetProperty("id").GetInt32(), Name: folder.GetProperty("name").GetString()))
            .Should().BeEquivalentTo(expected);
    }

    [Test]
    public async Task GetMusicFolders_RenamedSource_ShowsTheNewName()
    {
        string name = $"Renamed {HttpClientExtensions.Unique()}";
        HttpResponseMessage renamed = await _admin.PatchAsJsonAsync($"/api/v1/sources/{_second.Id}", new { name });
        renamed.StatusCode.Should().Be(HttpStatusCode.OK);
        try
        {
            JsonElement folders = (await JsonAsync("getMusicFolders", _profile.PasswordAuth))
                .GetProperty("musicFolders");

            folders.GetProperty("musicFolder").EnumerateArray()
                .Single(folder => folder.GetProperty("id").GetInt32() == SubsonicIds.MusicFolder(_second.Id))
                .GetProperty("name").GetString().Should().Be(name);
        }
        finally
        {
            await _admin.PatchAsJsonAsync($"/api/v1/sources/{_second.Id}", new { name = _second.Name });
        }
    }

    [Test]
    public async Task GetArtistsAndIndexes_WithFolder_ListOnlyTheArtistsOfTheSource()
    {
        (await ArtistsAsync("getArtists", "artists", string.Empty)).Should().Equal("Alpha", "Delta", "Gamma");
        (await ArtistsAsync("getArtists", "artists", "1")).Should().Equal("Alpha");
        (await ArtistsAsync("getArtists", "artists", SecondFolder)).Should().Equal("Gamma");
        (await ArtistsAsync("getIndexes", "indexes", SecondFolder)).Should().Equal("Gamma");
        (await ArtistsAsync("getIndexes", "indexes", "1")).Should().Equal("Alpha");
    }

    [Test]
    public async Task GetArtists_FolderOfADeactivatedSource_StillNarrowsTheList()
    {
        (await ArtistsAsync("getArtists", "artists", ArchiveFolder)).Should().Equal("Delta");
    }

    [TestCase("alphabeticalByName")]
    [TestCase("alphabeticalByArtist")]
    [TestCase("newest")]
    [TestCase("random")]
    [TestCase("byGenre&genre=Rock")]
    [TestCase("byYear&fromYear=2000&toYear=2002")]
    public async Task GetAlbumList2_WithFolder_ListsOnlyTheAlbumsOfTheSource(string type)
    {
        (await AlbumsAsync(_profile, type, string.Empty)).Should().BeEquivalentTo("One", "Three", "Four");
        (await AlbumsAsync(_profile, type, "1")).Should().Equal("One");
        (await AlbumsAsync(_profile, type, SecondFolder)).Should().Equal("Three");
    }

    [Test]
    public async Task GetAlbumList2_PlaysRatingsAndFavoritesWithFolder_CountOnlyTheSource()
    {
        SubsonicProfile profile = await CreateProfileAsync();
        foreach (string title in new[] { "First", "Fourth" })
        {
            TrackDto track = Track(title);
            (await StatusAsync("scrobble", $"{profile.PasswordAuth}&id={track.Id}")).Should().Be("ok");
            (await StatusAsync("setRating", $"{profile.PasswordAuth}&id={track.Id}&rating=4")).Should().Be("ok");
            (await StatusAsync("star", $"{profile.PasswordAuth}&albumId={track.Album!.Id}")).Should().Be("ok");
        }

        foreach (string type in new[] { "frequent", "recent", "highest", "starred" })
        {
            (await AlbumsAsync(profile, type, string.Empty)).Should().BeEquivalentTo(["One", "Three"], type);
            (await AlbumsAsync(profile, type, "1")).Should().Equal(["One"], type);
            (await AlbumsAsync(profile, type, SecondFolder)).Should().Equal(["Three"], type);
        }
    }

    [Test]
    public async Task GetRandomSongs_WithFolder_ListsOnlyTheSongsOfTheSource()
    {
        (await RandomSongsAsync(string.Empty)).Should().BeEquivalentTo("First", "Fourth", "Fifth");
        (await RandomSongsAsync("1")).Should().Equal("First");
        (await RandomSongsAsync($"{SecondFolder}&genre=Rock&fromYear=2000&toYear=2002")).Should().Equal("Fourth");
    }

    [Test]
    public async Task Search3_WithFolder_FindsOnlyTheSource()
    {
        JsonElement all = (await JsonAsync("search3", $"{_profile.PasswordAuth}&query=%22%22"))
            .GetProperty("searchResult3");
        JsonElement second = (await JsonAsync(
                "search3",
                $"{_profile.PasswordAuth}&query=%22%22&musicFolderId={SecondFolder}"))
            .GetProperty("searchResult3");

        all.GetProperty("song").GetArrayLength().Should().Be(3);
        Names(second, "artist").Should().Equal("Gamma");
        Names(second, "album").Should().Equal("Three");
        second.GetProperty("song").EnumerateArray().Select(song => song.GetProperty("title").GetString())
            .Should().Equal("Fourth");
    }

    [Test]
    public async Task GetStarred2_WithFolder_ListsOnlyTheFavoritesOfTheSource()
    {
        SubsonicProfile profile = await CreateProfileAsync();
        foreach (string title in new[] { "First", "Fourth" })
        {
            TrackDto track = Track(title);
            string ids = $"id={track.Id}&albumId={track.Album!.Id}&artistId={track.Artist!.Id}";
            (await StatusAsync("star", $"{profile.PasswordAuth}&{ids}")).Should().Be("ok");
        }

        JsonElement all = (await JsonAsync("getStarred2", profile.PasswordAuth)).GetProperty("starred2");
        JsonElement second = (await JsonAsync("getStarred2", $"{profile.PasswordAuth}&musicFolderId={SecondFolder}"))
            .GetProperty("starred2");

        all.GetProperty("song").GetArrayLength().Should().Be(2);
        Names(second, "artist").Should().Equal("Gamma");
        Names(second, "album").Should().Equal("Three");
        second.GetProperty("song").EnumerateArray().Select(song => song.GetProperty("title").GetString())
            .Should().Equal("Fourth");
    }

    [TestCase("getIndexes", "")]
    [TestCase("getArtists", "")]
    [TestCase("getAlbumList2", "&type=newest")]
    [TestCase("getRandomSongs", "")]
    [TestCase("search3", "&query=a")]
    [TestCase("getStarred2", "")]
    public async Task ListWithUnknownFolder_FailsWithNotFound(string endpoint, string query)
    {
        (await ErrorCodeAsync(endpoint, $"{_profile.PasswordAuth}{query}&musicFolderId=2")).Should().Be(70);
        (await ErrorCodeAsync(endpoint, $"{_profile.PasswordAuth}{query}&musicFolderId=music")).Should().Be(70);
    }

    private static string FolderOf(MediaSourceDto source)
    {
        return SubsonicIds.MusicFolder(source.Id).ToString(CultureInfo.InvariantCulture);
    }

    private TrackDto Track(string title)
    {
        return _tracks.Single(track => track.Title == title);
    }

    private async Task<IReadOnlyList<string?>> ArtistsAsync(string endpoint, string property, string folder)
    {
        JsonElement result = (await JsonAsync(endpoint, $"{_profile.PasswordAuth}{FolderParameter(folder)}"))
            .GetProperty(property);
        return [.. result.GetProperty("index").EnumerateArray()
            .SelectMany(index => index.GetProperty("artist").EnumerateArray())
            .Select(artist => artist.GetProperty("name").GetString())];
    }

    private async Task<IReadOnlyList<string?>> AlbumsAsync(SubsonicProfile profile, string type, string folder)
    {
        JsonElement list = (await JsonAsync(
                "getAlbumList2",
                $"{profile.PasswordAuth}&type={type}&size=10{FolderParameter(folder)}"))
            .GetProperty("albumList2");
        return Names(list, "album");
    }

    private async Task<IReadOnlyList<string?>> RandomSongsAsync(string folder)
    {
        JsonElement songs = (await JsonAsync("getRandomSongs", $"{_profile.PasswordAuth}{FolderParameter(folder)}"))
            .GetProperty("randomSongs");
        return [.. songs.GetProperty("song").EnumerateArray().Select(song => song.GetProperty("title").GetString())];
    }

    private static IReadOnlyList<string?> Names(JsonElement element, string list)
    {
        return element.TryGetProperty(list, out JsonElement items)
            ? [.. items.EnumerateArray().Select(item => item.GetProperty("name").GetString())]
            : [];
    }

    private static string FolderParameter(string folder)
    {
        return folder.Length == 0 ? string.Empty : $"&musicFolderId={folder}";
    }

    private async Task<MediaSourceDto> CreateSourceAsync(string prefix, string path)
    {
        HttpResponseMessage response = await _admin.PostAsJsonAsync(
            "/api/v1/sources",
            new { name = $"{prefix} {HttpClientExtensions.Unique()}", path });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        await _admin.WaitForJobsIdleAsync();
        return (await response.Content.ReadFromJsonAsync<MediaSourceDto>(HttpClientExtensions.JsonOptions))!;
    }

    /// <summary>
    /// A profile with the Subsonic access on and an app password.
    /// </summary>
    private async Task<SubsonicProfile> CreateProfileAsync()
    {
        string name = $"Folders {HttpClientExtensions.Unique()}";
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
        using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync());
        return document.RootElement.GetProperty("subsonic-response").Clone();
    }

    private async Task<string?> StatusAsync(string endpoint, string query)
    {
        return (await JsonAsync(endpoint, query)).GetProperty("status").GetString();
    }

    private async Task<int> ErrorCodeAsync(string endpoint, string query)
    {
        JsonElement root = await JsonAsync(endpoint, query);
        root.GetProperty("status").GetString().Should().Be("failed");
        return root.GetProperty("error").GetProperty("code").GetInt32();
    }

    private sealed record SubsonicProfile(string Name, string Password, HttpClient Client)
    {
        public string PasswordAuth => $"u={Uri.EscapeDataString(Name)}&p={Password}";
    }
}
