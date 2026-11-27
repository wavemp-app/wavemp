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
/// The tolerant search and the aliases of artists through the API: names typed differently, aliases added by the
/// admin and aliases taken from a simulated MusicBrainz, so no request leaves the process.
/// </summary>
[TestFixture]
[Category("Integration")]
public class ArtistAliasesApiTests
{
    private const string SettingsPath = "/api/v1/settings/artist-pictures";
    private const string MusicBrainz = "https://musicbrainz.org/ws/2/artist";

    private static readonly Guid _musicBrainzId = Guid.Parse("f4d5cc07-3bc9-4836-9b15-88a08359bc63");

    private TemporaryDirectory _media = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _listener = null!;

    private SimulatedWebHandler Web => _factory.ArtistLookups;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        await AudioAsync("P!nk/Funhouse/01.mp3", "P!nk", "Funhouse", "So What", _musicBrainzId.ToString());
        await AudioAsync("Ke$ha/Animal/01.mp3", "Ke$ha", "Animal", "Tik Tok");
        await AudioAsync("Rihanna/Loud/01.mp3", "Rihanna", "Loud", "Only Girl");
        _factory = new WaveMpWebApplicationFactory(
            "wavemp_tests_service_artist_aliases",
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
        await SwitchAsync("""{ "onlineLookupEnabled": false }""");
        Web.Reset();
        Web.OnJson($"{MusicBrainz}/{_musicBrainzId}?inc=url-rels+aliases", """
            { "name": "P!nk", "sort-name": "P!nk", "relations": [],
              "aliases": [ { "name": "Pink" }, { "name": "Alecia Moore" } ] }
            """);
    }

    [TestCase("pink", "P!nk", "Funhouse", "So What")]
    [TestCase("kesha", "Ke$ha", "Animal", "Tik Tok")]
    [TestCase("rihana", "Rihanna", "Loud", "Only Girl")]
    public async Task SearchLibrary_NameTypedDifferently_FindsTheArtistItsAlbumsAndSongs(
        string query,
        string artist,
        string album,
        string track)
    {
        SearchResultDto result = await _listener.GetJsonAsync<SearchResultDto>($"/api/v1/search?q={query}");

        result.Artists.Items.Select(item => item.Name).Should().StartWith(artist);
        result.Albums.Items.Select(item => item.Title).Should().Contain(album);
        result.Tracks.Items.Select(item => item.Title).Should().Contain(track);
    }

    [Test]
    public async Task Aliases_AddedByTheAdmin_AreFoundByTheSearchUntilRemoved()
    {
        ArtistDto artist = await ArtistAsync("Rihanna");
        string path = $"/api/v1/artists/{artist.Id}/aliases";

        HttpResponseMessage created = await _admin.PostAsJsonAsync(path, new { name = "  Robyn   Fenty " });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        ArtistAliasDto alias = (await created.Content.ReadFromJsonAsync<ArtistAliasDto>(
            HttpClientExtensions.JsonOptions))!;
        alias.Should().BeEquivalentTo(new { Name = "Robyn Fenty", Source = ArtistAliasSourceDto.Admin });
        created.Headers.Location!.AbsolutePath.Should().Be($"{path}/{alias.Id}");
        (await _listener.GetJsonAsync<ArtistAliasDto>($"{path}/{alias.Id}")).Should().BeEquivalentTo(alias);
        (await _listener.GetJsonAsync<PageDto<ArtistAliasDto>>(path)).Items.Should().ContainSingle();
        (await _listener.GetJsonAsync<SearchResultDto>("/api/v1/search?q=robyn%20fenty")).Artists.Items
            .Select(item => item.Name).Should().Equal("Rihanna");

        HttpResponseMessage duplicate = await _admin.PostAsJsonAsync(path, new { name = "ROBYN FENTY" });
        await duplicate.ShouldBeProblemAsync(HttpStatusCode.Conflict, "artist.alias_exists");

        (await _admin.DeleteAsync($"{path}/{alias.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await (await _admin.DeleteAsync($"{path}/{alias.Id}"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "artist.alias_not_found");
        (await _listener.GetJsonAsync<SearchResultDto>("/api/v1/search?q=robyn%20fenty")).Artists.Items
            .Should().BeEmpty();
    }

    [Test]
    public async Task CreateArtistAlias_AsListener_IsForbidden()
    {
        ArtistDto artist = await ArtistAsync("Rihanna");

        HttpResponseMessage response = await _listener.PostAsJsonAsync(
            $"/api/v1/artists/{artist.Id}/aliases",
            new { name = "Riri" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task CreateArtistAlias_WithoutName_IsValidationProblem(string name)
    {
        ArtistDto artist = await ArtistAsync("Rihanna");

        HttpResponseMessage response = await _admin.PostAsJsonAsync(
            $"/api/v1/artists/{artist.Id}/aliases",
            new { name });

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Code.Should().Be(ValidationErrorCodeDto.Required);
    }

    [Test]
    public async Task ListArtistAliases_UnknownArtist_IsNotFound()
    {
        HttpResponseMessage response = await _listener.GetAsync($"/api/v1/artists/{Guid.NewGuid()}/aliases");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "artist.not_found");
    }

    [Test]
    public async Task ImportArtistAliasesFromMusicBrainz_LookupOff_IsConflictWithoutRequest()
    {
        ArtistDto artist = await ArtistAsync("P!nk");

        HttpResponseMessage response = await _admin.PostAsync(
            $"/api/v1/artists/{artist.Id}/aliases/musicbrainz-imports",
            null);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "artist.picture_lookup_disabled");
        Web.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task ImportArtistAliasesFromMusicBrainz_ArtistWithoutMusicBrainzArtist_IsConflict()
    {
        await SwitchAsync("""{ "onlineLookupEnabled": true }""");
        ArtistDto artist = await ArtistAsync("Ke$ha");

        HttpResponseMessage response = await _admin.PostAsync(
            $"/api/v1/artists/{artist.Id}/aliases/musicbrainz-imports",
            null);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "artist.musicbrainz_artist_missing");
        Web.Requests.Should().BeEmpty();
    }

    [Test]
    public async Task ImportArtistAliasesFromMusicBrainz_KnownArtist_TakesTheAliasesTheSearchFinds()
    {
        await SwitchAsync("""{ "onlineLookupEnabled": true }""");
        ArtistDto artist = await ArtistAsync("P!nk");

        HttpResponseMessage response = await _admin.PostAsync(
            $"/api/v1/artists/{artist.Id}/aliases/musicbrainz-imports",
            null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        PageDto<ArtistAliasDto> aliases = (await response.Content.ReadFromJsonAsync<PageDto<ArtistAliasDto>>(
            HttpClientExtensions.JsonOptions))!;
        aliases.Items.Select(alias => (alias.Name, alias.Source)).Should().Equal(
            ("Alecia Moore", ArtistAliasSourceDto.MusicBrainz),
            ("Pink", ArtistAliasSourceDto.MusicBrainz));
        Web.Requests.Should().ContainSingle().Which.Url.Should().Be(
            $"{MusicBrainz}/{_musicBrainzId}?inc=url-rels+aliases&fmt=json");
        (await _listener.GetJsonAsync<SearchResultDto>("/api/v1/search?q=alecia")).Artists.Items
            .Select(item => item.Name).Should().Equal("P!nk");
    }

    private async Task SwitchAsync(string body)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync("PATCH", SettingsPath, body);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task<ArtistDto> ArtistAsync(string name)
    {
        PageDto<ArtistDto> artists = await _admin.GetJsonAsync<PageDto<ArtistDto>>("/api/v1/artists?limit=100");
        return artists.Items.Single(artist => artist.Name == name);
    }

    private Task AudioAsync(string path, string artist, string album, string title, string? musicBrainzId = null)
    {
        return TestMediaGenerator.CreateAudioAsync(
            _media.Combine(path),
            AudioFormat.Mp3,
            new TestTags { Artist = artist, Album = album, Title = title, MusicBrainzArtistId = musicBrainzId });
    }
}
