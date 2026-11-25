using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Smart playlists of profiles through the API: their rules, their songs computed from the current library, the
/// templates and the conversion into a normal playlist, with a small library of synthetic media.
/// </summary>
[TestFixture]
[Category("Integration")]
public class SmartPlaylistsApiTests
{
    private const string Path = "/api/v1/me/smart-playlists";

    private TemporaryDirectory _media = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private IReadOnlyList<TrackDto> _tracks = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        await CreateTrackAsync("Alpha", "One", "First", "Rock", withCover: true);
        await CreateTrackAsync("Alpha", "One", "Second", "Rock", withCover: false);
        await CreateTrackAsync("Beta", "Two", "Third", "Pop", withCover: true);
        await CreateTrackAsync("Gamma", "Three", "Fourth", "Pop", withCover: false);

        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_smart_playlists",
            freshDatabase: true,
            mediaPath: _media.Path);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();

        HttpResponseMessage response = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        ScanDto scan = await _admin.WaitForScanAsync(queued.Id);
        scan.Status.Should().Be(JobRunStatusDto.Succeeded);

        _tracks = (await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100")).Items;
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
        _media.Dispose();
    }

    [Test]
    public async Task SmartPlaylists_WithoutToken_ReturnUnauthorized()
    {
        HttpResponseMessage listed = await _anonymous.GetAsync(Path);
        HttpResponseMessage created = await _anonymous.PostAsJsonAsync(
            Path,
            new { name = "Mix", rules = Array.Empty<object>() });
        HttpResponseMessage tracks = await _anonymous.GetAsync($"{Path}/{Guid.NewGuid()}/tracks");
        HttpResponseMessage converted = await _anonymous.PostAsync($"{Path}/{Guid.NewGuid()}/conversions", null);
        HttpResponseMessage templates = await _anonymous.GetAsync("/api/v1/smart-playlist-templates");

        await listed.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await created.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await tracks.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await converted.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await templates.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task ListSmartPlaylistTemplates_AllTemplates_InTheirOrderWithTheirRules()
    {
        using HttpClient client = await CreateProfileAsync();

        PageDto<SmartPlaylistTemplateDto> page =
            await client.GetJsonAsync<PageDto<SmartPlaylistTemplateDto>>("/api/v1/smart-playlist-templates");
        PageDto<SmartPlaylistTemplateDto> second =
            await client.GetJsonAsync<PageDto<SmartPlaylistTemplateDto>>(
                "/api/v1/smart-playlist-templates?offset=1&limit=1");

        page.Total.Should().Be(5);
        page.Items.Select(template => template.Id).Should().Equal(
            SmartPlaylistTemplateIdDto.FourStarsAndMore,
            SmartPlaylistTemplateIdDto.NeverPlayed,
            SmartPlaylistTemplateIdDto.RecentlyAdded,
            SmartPlaylistTemplateIdDto.MostPlayed,
            SmartPlaylistTemplateIdDto.Favorites);
        page.Items[0].Rules.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = SmartPlaylistFieldDto.Rating,
            Operator = SmartPlaylistOperatorDto.AtLeast,
            Number = 4,
        });
        page.Items[3].Limit.Should().Be(50);
        second.Items.Should().ContainSingle().Which.Id.Should().Be(SmartPlaylistTemplateIdDto.NeverPlayed);
    }

    [Test]
    public async Task CreateMySmartPlaylist_WithRules_PicksTheMatchingTracksInItsOrder()
    {
        using HttpClient client = await CreateProfileAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(Path, new
        {
            name = "  Rock   and Gamma ",
            match = "Any",
            rules = new object[]
            {
                new { field = "Genre", @operator = "Is", text = "rock" },
                new { field = "Artist", @operator = "Contains", text = "amm" },
            },
            sort = "Title",
            direction = "Desc",
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        SmartPlaylistDto playlist =
            (await response.Content.ReadFromJsonAsync<SmartPlaylistDto>(HttpClientExtensions.JsonOptions))!;
        response.Headers.Location!.AbsolutePath.Should().Be($"{Path}/{playlist.Id}");
        playlist.Name.Should().Be("Rock and Gamma");
        playlist.Match.Should().Be(SmartPlaylistMatchDto.Any);
        playlist.Sort.Should().Be(SmartPlaylistTrackSortDto.Title);
        playlist.Direction.Should().Be(SortDirectionDto.Desc);
        playlist.Limit.Should().BeNull();
        playlist.TrackCount.Should().Be(3);
        playlist.DurationMilliseconds.Should().BePositive();
        playlist.Cover.Kind.Should().Be(PlaylistCoverKindDto.Tracks);
        playlist.Cover.TrackIds.Should().Equal(Track("Second"));
        (await client.GetJsonAsync<SmartPlaylistDto>($"{Path}/{playlist.Id}")).Should().BeEquivalentTo(playlist);
        (await TitlesAsync(client, playlist.Id)).Should().Equal("Second", "Fourth", "First");
    }

    [Test]
    public async Task CreateMySmartPlaylist_WithoutOptionalValues_UsesTheDefaults()
    {
        using HttpClient client = await CreateProfileAsync();

        SmartPlaylistDto playlist = await CreateAsync(
            client,
            new { name = "Everything", rules = Array.Empty<object>() });

        playlist.Match.Should().Be(SmartPlaylistMatchDto.All);
        playlist.Sort.Should().Be(SmartPlaylistTrackSortDto.Artist);
        playlist.Direction.Should().Be(SortDirectionDto.Asc);
        playlist.TrackCount.Should().Be(_tracks.Count);
    }

    [Test]
    public async Task ListMySmartPlaylistTracks_RatingsAndFavoritesChange_ReflectsTheCurrentState()
    {
        using HttpClient client = await CreateProfileAsync();
        SmartPlaylistDto favorites = await CreateAsync(client, new
        {
            name = "Favorites",
            rules = new[] { new { field = "Favorite", @operator = "IsTrue" } },
            sort = "Title",
        });
        SmartPlaylistDto rated = await CreateAsync(client, new
        {
            name = "Good",
            rules = new[] { new { field = "Rating", @operator = "AtLeast", number = 4 } },
            sort = "Rating",
            direction = "Desc",
        });
        (await TitlesAsync(client, favorites.Id)).Should().BeEmpty();

        (await client.PutAsync($"/api/v1/me/favorites/tracks/{Track("Third")}", null)).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/v1/tracks/{Track("First")}/rating", new { rating = 4 }))
            .EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync($"/api/v1/tracks/{Track("Fourth")}/rating", new { rating = 5 }))
            .EnsureSuccessStatusCode();

        (await TitlesAsync(client, favorites.Id)).Should().Equal("Third");
        (await TitlesAsync(client, rated.Id)).Should().Equal("Fourth", "First");
        (await client.GetJsonAsync<SmartPlaylistDto>($"{Path}/{rated.Id}")).TrackCount.Should().Be(2);
    }

    [Test]
    public async Task ListMySmartPlaylistTracks_WithLimitAndPaging_ReturnsThePagesUpToTheLimit()
    {
        using HttpClient client = await CreateProfileAsync();
        SmartPlaylistDto playlist = await CreateAsync(client, new
        {
            name = "Three",
            rules = Array.Empty<object>(),
            sort = "Title",
            limit = 3,
        });

        PageDto<TrackDto> first = await client.GetJsonAsync<PageDto<TrackDto>>($"{Path}/{playlist.Id}/tracks?limit=2");
        PageDto<TrackDto> second =
            await client.GetJsonAsync<PageDto<TrackDto>>($"{Path}/{playlist.Id}/tracks?offset=2&limit=2");

        playlist.TrackCount.Should().Be(3);
        first.Total.Should().Be(3);
        first.Items.Select(track => track.Title).Should().Equal("First", "Fourth");
        second.Items.Select(track => track.Title).Should().Equal("Second");
    }

    [Test]
    public async Task CreateMySmartPlaylist_InvalidRules_ReturnsTheFieldsWithTheirCodes()
    {
        using HttpClient client = await CreateProfileAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(Path, new
        {
            name = "Broken",
            rules = new object[]
            {
                new { field = "Rating", @operator = "Contains", text = "5" },
                new { field = "Year", @operator = "Between", number = 2000, numberTo = 1990 },
                new { field = "Genre", @operator = "Is" },
                new { field = "Rating", @operator = "AtLeast", number = 9 },
            },
        });

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().BeEquivalentTo(
        [
            new { Field = "rules[0].operator", Code = ValidationErrorCodeDto.InvalidValue },
            new { Field = "rules[1].numberTo", Code = ValidationErrorCodeDto.OutOfRange },
            new { Field = "rules[2].text", Code = ValidationErrorCodeDto.Required },
            new { Field = "rules[3].number", Code = ValidationErrorCodeDto.OutOfRange },
        ]);
        errors[1].Min.Should().Be(2000);
        errors[1].Max.Should().Be(9999);
        errors[3].Max.Should().Be(5);
    }

    [TestCase("{\"rules\":[]}", "name", ValidationErrorCodeDto.Required)]
    [TestCase("{\"name\":\"   \",\"rules\":[]}", "name", ValidationErrorCodeDto.Required)]
    [TestCase("{\"name\":\"Mix\"}", "rules", ValidationErrorCodeDto.Required)]
    [TestCase("{\"name\":\"Mix\",\"rules\":[],\"limit\":0}", "limit", ValidationErrorCodeDto.OutOfRange)]
    [TestCase(
        "{\"name\":\"Mix\",\"rules\":[{\"operator\":\"IsTrue\"}]}",
        "rules[0].field",
        ValidationErrorCodeDto.Required)]
    public async Task CreateMySmartPlaylist_InvalidRequest_ReturnsTheField(
        string body,
        string field,
        ValidationErrorCodeDto code)
    {
        using HttpClient client = await CreateProfileAsync();

        HttpResponseMessage response = await client.SendJsonAsync("POST", Path, body);

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Field = field, Code = code });
    }

    [Test]
    public async Task CreateMySmartPlaylist_TooManyRules_ReturnsMaxItems()
    {
        using HttpClient client = await CreateProfileAsync();

        HttpResponseMessage response = await client.PostAsJsonAsync(Path, new
        {
            name = "Long",
            rules = Enumerable.Repeat(new { field = "HasAudio", @operator = "IsTrue" }, 21),
        });

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "rules",
            Code = ValidationErrorCodeDto.MaxItems,
            Max = 20,
        });
    }

    [Test]
    public async Task UpdateMySmartPlaylist_NewRules_ReplacesThemAndKeepsTheCreationTime()
    {
        using HttpClient client = await CreateProfileAsync();
        SmartPlaylistDto created = await CreateAsync(client, new
        {
            name = "Before",
            rules = new[] { new { field = "Genre", @operator = "Is", text = "Rock" } },
        });

        HttpResponseMessage response = await client.PutAsJsonAsync($"{Path}/{created.Id}", new
        {
            name = "After",
            rules = new[] { new { field = "Genre", @operator = "IsNot", text = "Rock" } },
            sort = "Title",
            limit = 1,
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        SmartPlaylistDto updated =
            (await response.Content.ReadFromJsonAsync<SmartPlaylistDto>(HttpClientExtensions.JsonOptions))!;
        updated.Name.Should().Be("After");
        updated.Rules.Should().ContainSingle().Which.Operator.Should().Be(SmartPlaylistOperatorDto.IsNot);
        updated.Limit.Should().Be(1);
        updated.TrackCount.Should().Be(1);
        updated.CreatedAt.Should().Be(created.CreatedAt);
        updated.UpdatedAt.Should().BeOnOrAfter(created.UpdatedAt);
        (await TitlesAsync(client, created.Id)).Should().Equal("Fourth");
    }

    [Test]
    public async Task SmartPlaylists_OfAnotherProfile_AreNotFound()
    {
        using HttpClient owner = await CreateProfileAsync();
        using HttpClient other = await CreateProfileAsync();
        SmartPlaylistDto playlist = await CreateAsync(owner, new { name = "Private", rules = Array.Empty<object>() });
        object body = new { name = "Taken", rules = Array.Empty<object>() };

        HttpResponseMessage get = await other.GetAsync($"{Path}/{playlist.Id}");
        HttpResponseMessage tracks = await other.GetAsync($"{Path}/{playlist.Id}/tracks");
        HttpResponseMessage update = await other.PutAsJsonAsync($"{Path}/{playlist.Id}", body);
        HttpResponseMessage convert = await other.PostAsync($"{Path}/{playlist.Id}/conversions", null);
        HttpResponseMessage delete = await other.DeleteAsync($"{Path}/{playlist.Id}");
        PageDto<SmartPlaylistDto> listed = await other.GetJsonAsync<PageDto<SmartPlaylistDto>>(Path);

        await get.ShouldBeProblemAsync(HttpStatusCode.NotFound, "playlist.smart_not_found");
        await tracks.ShouldBeProblemAsync(HttpStatusCode.NotFound, "playlist.smart_not_found");
        await update.ShouldBeProblemAsync(HttpStatusCode.NotFound, "playlist.smart_not_found");
        await convert.ShouldBeProblemAsync(HttpStatusCode.NotFound, "playlist.smart_not_found");
        await delete.ShouldBeProblemAsync(HttpStatusCode.NotFound, "playlist.smart_not_found");
        listed.Items.Should().BeEmpty();
        (await owner.GetJsonAsync<SmartPlaylistDto>($"{Path}/{playlist.Id}")).Name.Should().Be("Private");
    }

    [Test]
    public async Task ListMySmartPlaylists_SortedAndSearched_ReturnsTheSmartPlaylistsOfTheProfile()
    {
        using HttpClient client = await CreateProfileAsync();
        await CreateAsync(client, new { name = "beta", rules = Array.Empty<object>() });
        await CreateAsync(client, new { name = "Alpha", rules = Array.Empty<object>() });

        PageDto<SmartPlaylistDto> all = await client.GetJsonAsync<PageDto<SmartPlaylistDto>>(Path);
        PageDto<SmartPlaylistDto> desc = await client.GetJsonAsync<PageDto<SmartPlaylistDto>>($"{Path}?direction=Desc");
        PageDto<SmartPlaylistDto> searched = await client.GetJsonAsync<PageDto<SmartPlaylistDto>>($"{Path}?q=ALP");

        all.Items.Select(item => item.Name).Should().Equal("Alpha", "beta");
        all.Items.Should().AllSatisfy(item => item.TrackCount.Should().Be(_tracks.Count));
        desc.Items.Select(item => item.Name).Should().Equal("beta", "Alpha");
        searched.Items.Select(item => item.Name).Should().Equal("Alpha");
    }

    [Test]
    public async Task DeleteMySmartPlaylist_Existing_RemovesIt()
    {
        using HttpClient client = await CreateProfileAsync();
        SmartPlaylistDto playlist = await CreateAsync(client, new { name = "Gone", rules = Array.Empty<object>() });

        HttpResponseMessage deleted = await client.DeleteAsync($"{Path}/{playlist.Id}");
        HttpResponseMessage again = await client.DeleteAsync($"{Path}/{playlist.Id}");

        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await again.ShouldBeProblemAsync(HttpStatusCode.NotFound, "playlist.smart_not_found");
    }

    [Test]
    public async Task ConvertMySmartPlaylist_Existing_CreatesAPlaylistWithItsTracksAndDeletesIt()
    {
        using HttpClient client = await CreateProfileAsync();
        SmartPlaylistDto smart = await CreateAsync(client, new
        {
            name = "Pop Picks",
            rules = new[] { new { field = "Genre", @operator = "Is", text = "Pop" } },
            sort = "Title",
        });

        HttpResponseMessage response = await client.PostAsync($"{Path}/{smart.Id}/conversions", null);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        PlaylistDto playlist =
            (await response.Content.ReadFromJsonAsync<PlaylistDto>(HttpClientExtensions.JsonOptions))!;
        response.Headers.Location!.AbsolutePath.Should().Be($"/api/v1/me/playlists/{playlist.Id}");
        playlist.Name.Should().Be("Pop Picks");
        playlist.TrackCount.Should().Be(2);
        PageDto<PlaylistEntryDto> entries =
            await client.GetJsonAsync<PageDto<PlaylistEntryDto>>($"/api/v1/me/playlists/{playlist.Id}/tracks");
        entries.Items.Select(entry => entry.Track.Title).Should().Equal("Fourth", "Third");
        await (await client.GetAsync($"{Path}/{smart.Id}"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "playlist.smart_not_found");
    }

    private Guid Track(string title)
    {
        return _tracks.Single(track => track.Title == title).Id;
    }

    private async Task CreateTrackAsync(string artist, string album, string title, string genre, bool withCover)
    {
        if (withCover)
        {
            await TestMediaGenerator.CreateImageAsync(_media.Combine($"{artist}/{album}/cover.jpg"), 300, 300);
        }

        await TestMediaGenerator.CreateAudioAsync(
            _media.Combine($"{artist}/{album}/{title}.mp3"),
            AudioFormat.Mp3,
            new TestTags { Title = title, Artist = artist, Album = album, Genre = genre });
    }

    private async Task<HttpClient> CreateProfileAsync()
    {
        UserDto user = await _admin.CreateUserAsync(new { name = $"Smart {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        return _factory.CreateAuthorizedClient(session.Token);
    }

    private static async Task<SmartPlaylistDto> CreateAsync(HttpClient client, object body)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(Path, body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<SmartPlaylistDto>(HttpClientExtensions.JsonOptions))!;
    }

    private static async Task<IReadOnlyList<string>> TitlesAsync(HttpClient client, Guid id)
    {
        PageDto<TrackDto> page = await client.GetJsonAsync<PageDto<TrackDto>>($"{Path}/{id}/tracks?limit=100");
        return [.. page.Items.Select(track => track.Title)];
    }
}
