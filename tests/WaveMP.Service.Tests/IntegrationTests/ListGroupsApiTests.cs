using System.Net;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WaveMP.Data;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Seeds;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// The groups of the lists of tracks, albums and artists through the API, on a library written straight into the
/// database of the service.
/// </summary>
[TestFixture]
[Category("Integration")]
public class ListGroupsApiTests
{
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _user = null!;
    private Library _library = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new WaveMpWebApplicationFactory(
            "wavemp_tests_service_groups",
            freshDatabase: true);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        UserDto user = await _admin.CreateUserAsync(new { name = $"Listener {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        _user = _factory.CreateAuthorizedClient(session.Token);
        _library = await SeedAsync();

        HttpResponseMessage favorite = await _user.PutAsync(
            $"/api/v1/me/favorites/albums/{_library.Second.Id}",
            null);
        favorite.IsSuccessStatusCode.Should().BeTrue();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _user?.Dispose();
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
    }

    [TestCase("/api/v1/tracks/groups")]
    [TestCase("/api/v1/albums/groups")]
    [TestCase("/api/v1/artists/groups")]
    public async Task ListGroups_WithoutToken_ReturnUnauthorized(string path)
    {
        HttpResponseMessage response = await _anonymous.GetAsync(path);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task ListTrackGroups_ByDefault_GroupsTheTracksOfTheListByAlbumThenByArtist()
    {
        PageDto<ListGroupDto> page = await _user.GetJsonAsync<PageDto<ListGroupDto>>("/api/v1/tracks/groups");

        page.Should().BeEquivalentTo(new { Offset = 0, Limit = 1000, Total = 9 });
        page.Items.Select(group => (group.Kind, group.Position, group.Count, group.Letter)).Should().Equal(
            (ListGroupKindDto.Artist, 0, 1, "#"),
            (ListGroupKindDto.Album, 1, 3, "A"),
            (ListGroupKindDto.Album, 4, 2, "A"),
            (ListGroupKindDto.Artist, 6, 1, "A"),
            (ListGroupKindDto.Album, 7, 2, "B"),
            (ListGroupKindDto.Album, 9, 1, "E"),
            (ListGroupKindDto.Artist, 10, 1, "E"),
            (ListGroupKindDto.Album, 11, 2, null),
            (ListGroupKindDto.Unknown, 13, 1, null));
        page.Items[1].Should().BeEquivalentTo(new
        {
            Album = new
            {
                _library.First.Id,
                Title = "First Album",
                Artist = new { _library.Alpha.Id, Name = "Alpha Band" },
                IsCompilation = false,
            },
            Artist = (object?)null,
            Year = (int?)null,
            HasCover = true,
        });
        page.Items[6].Artist.Should().BeEquivalentTo(new { _library.Ebene.Id, Name = "\u00c9b\u00e8ne" });
        page.Items[7].Should().BeEquivalentTo(new
        {
            Album = new { Title = "Hits", Artist = (object?)null, IsCompilation = true },
            HasCover = false,
        });
        page.Items[8].Should().BeEquivalentTo(new { Album = (object?)null, Artist = (object?)null });
    }

    [TestCase("")]
    [TestCase("sort=Title&direction=Desc")]
    [TestCase("sort=Year")]
    [TestCase("sort=Duration&direction=Asc")]
    [TestCase("genre=pop")]
    public async Task ListTrackGroups_AnySortAndFilter_StartsEveryGroupAtItsOffsetInTheList(string query)
    {
        PageDto<ListGroupDto> groups = await _user.GetJsonAsync<PageDto<ListGroupDto>>(
            $"/api/v1/tracks/groups?{query}");
        PageDto<TrackDto> tracks = await _user.GetJsonAsync<PageDto<TrackDto>>($"/api/v1/tracks?limit=100&{query}");

        groups.Items.Sum(group => group.Count).Should().Be(tracks.Total);
        foreach (ListGroupDto group in groups.Items)
        {
            PageDto<TrackDto> part = await _user.GetJsonAsync<PageDto<TrackDto>>(
                $"/api/v1/tracks?offset={group.Position}&limit={group.Count}&{query}");
            part.Items.Should().HaveCount(group.Count).And.OnlyContain(track => track.Album != null
                ? group.Album != null && group.Album.Id == track.Album.Id
                : track.Artist != null
                    ? group.Album == null && group.Artist != null && group.Artist.Id == track.Artist.Id
                    : group.Kind == ListGroupKindDto.Unknown);
            part.Items.Should().Equal(tracks.Items.Skip(group.Position).Take(group.Count), SameTrack);
        }
    }

    [Test]
    public async Task ListTrackGroups_SortedByYear_CarriesTheYear()
    {
        PageDto<ListGroupDto> page = await _user.GetJsonAsync<PageDto<ListGroupDto>>(
            "/api/v1/tracks/groups?sort=Year");

        page.Items.Select(group => group.Year).Should().StartWith([2001, 2001]);
        page.Items.Should().OnlyContain(group => group.Letter == null);
    }

    [Test]
    public async Task ListTrackGroups_WithPaging_ReturnsTheRequestedGroups()
    {
        PageDto<ListGroupDto> page = await _user.GetJsonAsync<PageDto<ListGroupDto>>(
            "/api/v1/tracks/groups?offset=2&limit=3");

        page.Should().BeEquivalentTo(new { Offset = 2, Limit = 3, Total = 9 });
        page.Items.Select(group => group.Position).Should().Equal(4, 6, 7);
    }

    [Test]
    public async Task ListTrackGroups_WithFiltersAndMyMusic_GroupsOnlyTheMatchingTracks()
    {
        PageDto<ListGroupDto> ofAlpha = await _user.GetJsonAsync<PageDto<ListGroupDto>>(
            $"/api/v1/tracks/groups?artistId={_library.Alpha.Id}");
        PageDto<ListGroupDto> mine = await _user.GetJsonAsync<PageDto<ListGroupDto>>(
            "/api/v1/tracks/groups?myMusic=true");
        PageDto<ListGroupDto> ofAdmin = await _admin.GetJsonAsync<PageDto<ListGroupDto>>(
            "/api/v1/tracks/groups?myMusic=true");

        // The hits are on an album without album artist, one of them by the band.
        ofAlpha.Items.Select(group => (group.Album!.Id, group.Position, group.Count)).Should().Equal(
            (_library.First.Id, 0, 3),
            (_library.Second.Id, 3, 2),
            (_library.Hits.Id, 5, 1));
        mine.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Kind = ListGroupKindDto.Album,
            Position = 0,
            Count = 2,
            Album = new { _library.Second.Id },
        });
        ofAdmin.Total.Should().Be(0);
    }

    [Test]
    public async Task ListAlbumGroups_ByDefault_GroupsByAlbumArtist()
    {
        PageDto<ListGroupDto> page = await _user.GetJsonAsync<PageDto<ListGroupDto>>("/api/v1/albums/groups");

        page.Should().BeEquivalentTo(new { Offset = 0, Limit = 1000, Total = 4 });
        page.Items.Select(group => (group.Kind, group.Position, group.Count, group.Letter, group.Artist?.Id)).Should()
            .Equal(
                (ListGroupKindDto.Artist, 0, 2, "A", _library.Alpha.Id),
                (ListGroupKindDto.Artist, 2, 1, "B", _library.Beta.Id),
                (ListGroupKindDto.Artist, 3, 1, "E", _library.Ebene.Id),
                (ListGroupKindDto.Unknown, 4, 1, null, (Guid?)null));
        page.Items.Select(group => group.Artist?.Name).Should().Equal(
            "Alpha Band",
            "beta Singer",
            "\u00c9b\u00e8ne",
            null);
        page.Items.Should().OnlyContain(group => group.Album == null && group.Year == null && !group.HasCover);
    }

    [TestCase("sort=Title", new[] { "#", "E", "F", "H", "S" })]
    [TestCase("sort=Title&direction=Desc", new[] { "S", "H", "F", "E", "#" })]
    public async Task ListAlbumGroups_ByTitle_GroupsByIndexLetter(string query, string[] letters)
    {
        PageDto<ListGroupDto> page = await _user.GetJsonAsync<PageDto<ListGroupDto>>(
            $"/api/v1/albums/groups?{query}");

        page.Items.Should().OnlyContain(group => group.Kind == ListGroupKindDto.Letter && group.Count == 1);
        page.Items.Select(group => group.Letter).Should().Equal(letters);
        page.Items.Select(group => group.Position).Should().Equal(0, 1, 2, 3, 4);
    }

    [Test]
    public async Task ListAlbumGroups_ByYear_GroupsByYearWithUnknownYearsLast()
    {
        PageDto<ListGroupDto> page = await _user.GetJsonAsync<PageDto<ListGroupDto>>(
            "/api/v1/albums/groups?sort=Year");

        page.Items.Select(group => (group.Kind, group.Position, group.Count, group.Year)).Should().Equal(
            (ListGroupKindDto.Year, 0, 2, 2001),
            (ListGroupKindDto.Year, 2, 1, 2005),
            (ListGroupKindDto.Year, 3, 1, 2010),
            (ListGroupKindDto.Unknown, 4, 1, (int?)null));
    }

    [TestCase("sort=Artist")]
    [TestCase("sort=Year&direction=Desc")]
    [TestCase("sort=Title&genre=pop")]
    public async Task ListAlbumGroups_AnyGroupedSort_StartsEveryGroupAtItsOffsetInTheList(string query)
    {
        PageDto<ListGroupDto> groups = await _user.GetJsonAsync<PageDto<ListGroupDto>>(
            $"/api/v1/albums/groups?{query}");
        PageDto<AlbumDto> albums = await _user.GetJsonAsync<PageDto<AlbumDto>>($"/api/v1/albums?limit=100&{query}");

        groups.Items.Sum(group => group.Count).Should().Be(albums.Total);
        foreach (ListGroupDto group in groups.Items)
        {
            PageDto<AlbumDto> part = await _user.GetJsonAsync<PageDto<AlbumDto>>(
                $"/api/v1/albums?offset={group.Position}&limit={group.Count}&{query}");
            part.Items.Select(album => album.Id).Should().Equal(
                albums.Items.Skip(group.Position).Take(group.Count).Select(album => album.Id));
        }
    }

    [TestCase("sort=FirstSeenAt")]
    [TestCase("sort=Relevance&q=album")]
    [TestCase("q=album")]
    public async Task ListAlbumGroups_SortWithoutGroups_ReturnsAnEmptyPage(string query)
    {
        PageDto<ListGroupDto> page = await _user.GetJsonAsync<PageDto<ListGroupDto>>(
            $"/api/v1/albums/groups?{query}");

        page.Should().BeEquivalentTo(new { Items = Array.Empty<ListGroupDto>(), Total = 0 });
    }

    [Test]
    public async Task ListAlbumGroups_MyMusic_GroupsOnlyTheAlbumsOfTheProfile()
    {
        PageDto<ListGroupDto> page = await _user.GetJsonAsync<PageDto<ListGroupDto>>(
            "/api/v1/albums/groups?myMusic=true");

        page.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Kind = ListGroupKindDto.Artist,
            Position = 0,
            Count = 1,
            Artist = new { _library.Alpha.Id },
        });
    }

    [TestCase("", new[] { "#", "A", "B", "E" }, new[] { 0, 1, 3, 4 }, new[] { 1, 2, 1, 1 })]
    [TestCase("direction=Desc", new[] { "E", "B", "A", "#" }, new[] { 0, 1, 2, 4 }, new[] { 1, 1, 2, 1 })]
    [TestCase("myMusic=true", new[] { "A" }, new[] { 0 }, new[] { 1 })]
    public async Task ListArtistGroups_ByName_GroupsByIndexLetter(
        string query,
        string[] letters,
        int[] positions,
        int[] counts)
    {
        PageDto<ListGroupDto> page = await _user.GetJsonAsync<PageDto<ListGroupDto>>(
            $"/api/v1/artists/groups?{query}");

        page.Items.Should().OnlyContain(group => group.Kind == ListGroupKindDto.Letter);
        page.Items.Select(group => group.Letter).Should().Equal(letters);
        page.Items.Select(group => group.Position).Should().Equal(positions);
        page.Items.Select(group => group.Count).Should().Equal(counts);
    }

    [TestCase("sort=FirstSeenAt")]
    [TestCase("q=alpha")]
    public async Task ListArtistGroups_SortWithoutGroups_ReturnsAnEmptyPage(string query)
    {
        PageDto<ListGroupDto> page = await _user.GetJsonAsync<PageDto<ListGroupDto>>(
            $"/api/v1/artists/groups?{query}");

        page.Total.Should().Be(0);
        page.Items.Should().BeEmpty();
    }

    [TestCase("/api/v1/tracks/groups?sort=Foo")]
    [TestCase("/api/v1/tracks/groups?sort=42")]
    [TestCase("/api/v1/tracks/groups?direction=Up")]
    [TestCase("/api/v1/tracks/groups?offset=-1")]
    [TestCase("/api/v1/tracks/groups?limit=0")]
    [TestCase("/api/v1/tracks/groups?year=0")]
    [TestCase("/api/v1/tracks/groups?artistId=abc")]
    [TestCase("/api/v1/tracks/groups?hasVideo=maybe")]
    [TestCase("/api/v1/albums/groups?sort=Name")]
    [TestCase("/api/v1/albums/groups?limit=0")]
    [TestCase("/api/v1/albums/groups?year=10000")]
    [TestCase("/api/v1/artists/groups?sort=Title")]
    [TestCase("/api/v1/artists/groups?offset=-1")]
    [TestCase("/api/v1/artists/groups?myMusic=maybe")]
    public async Task ListGroups_WithInvalidQuery_ReturnBadRequest(string path)
    {
        HttpResponseMessage response = await _user.GetAsync(path);

        await response.ShouldBeValidationProblemAsync();
    }

    [TestCase("/api/v1/tracks/groups")]
    [TestCase("/api/v1/albums/groups")]
    [TestCase("/api/v1/artists/groups")]
    public async Task ListGroups_LimitAboveTheMaximum_ReturnBadRequestWithTheRange(string path)
    {
        HttpResponseMessage response = await _user.GetAsync($"{path}?limit=1001");

        (await response.ShouldBeValidationProblemAsync()).Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Field = "limit", Code = ValidationErrorCodeDto.OutOfRange, Min = 1, Max = 1000 });
    }

    [TestCase("/api/v1/tracks/groups")]
    [TestCase("/api/v1/albums/groups")]
    [TestCase("/api/v1/artists/groups")]
    public async Task ListGroups_TooLongSearch_ReturnBadRequest(string path)
    {
        HttpResponseMessage response = await _user.GetAsync($"{path}?q={new string('a', 201)}");

        (await response.ShouldBeValidationProblemAsync()).Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { Field = "q", Code = ValidationErrorCodeDto.MaxLength, Max = 200 });
    }

    private static bool SameTrack(TrackDto actual, TrackDto expected)
    {
        return actual.Id == expected.Id;
    }

    /// <summary>
    /// Five albums of three artists and four tracks without album:
    /// <list type="bullet">
    /// <item>First Album (Alpha Band, 2001, cover): three tracks; Second Album (Alpha Band, 2005): two tracks</item>
    /// <item>3rd Album (beta Singer, 2001, Pop): two tracks; Echo Album (Ebene with accents, 2010): one track</item>
    /// <item>Hits (compilation without album artist and year, Pop): one track of Alpha Band, one of beta Singer</item>
    /// <item>without album: Count (2 Many), Lonely (Another One), Single (Ebene), Nobody's Song (no artist)</item>
    /// </list>
    /// </summary>
    private async Task<Library> SeedAsync()
    {
        LibrarySeed seed = new();
        Artist alpha = seed.Artist("Alpha Band");
        Artist another = seed.Artist("Another One");
        Artist beta = seed.Artist("beta Singer");
        Artist ebene = seed.Artist("\u00c9b\u00e8ne");
        Artist many = seed.Artist("2 Many");
        Album first = seed.Album("First Album", alpha, year: 2001);
        Album second = seed.Album("Second Album", alpha, year: 2005);
        Album third = seed.Album("3rd Album", beta, year: 2001);
        Album echo = seed.Album("Echo Album", ebene, year: 2010);
        Album hits = seed.Album("Hits", isCompilation: true);

        seed.Track("Opening", alpha, first, 1, year: 2001, genre: "Rock");
        seed.Track("Middle", alpha, first, 2, year: 2001, genre: "Rock");
        seed.Track("Closing", alpha, first, 3, year: 2001, genre: "Rock");
        seed.Track("Return", alpha, second, 1, year: 2005, genre: "Rock");
        seed.Track("Farewell", alpha, second, 2, year: 2005, genre: "Rock");
        seed.Track("Ballad", beta, third, 1, year: 2001, genre: "Pop");
        seed.Track("Encore", beta, third, 2, year: 2001, genre: "Pop");
        seed.Track("Echo", ebene, echo, 1, year: 2010);
        seed.Track("Hit One", alpha, hits, 1, genre: "Pop");
        seed.Track("Hit Two", beta, hits, 2, genre: "Pop");
        seed.Track("Count", many);
        seed.Track("Lonely", another);
        seed.Track("Single", ebene);
        seed.Track("Nobody's Song");
        seed.AlbumCover(first, seed.File("Alpha Band/First Album/cover.jpg", MediaFileKind.Image, isPrimary: false));

        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        await seed.SaveAsync(scope.ServiceProvider.GetRequiredService<WaveMpDbContext>());
        return new Library(alpha, beta, ebene, first, second, hits);
    }

    private sealed record Library(Artist Alpha, Artist Beta, Artist Ebene, Album First, Album Second, Album Hits);
}
