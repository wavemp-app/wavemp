using AwesomeAssertions;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class AlbumRepositoryTests : LibraryDatabaseTests
{
    protected override string DatabaseName => "wavemp_tests_data_library_albums";

    [Test]
    public async Task ListAsync_Always_SummarizesTheTracksOfEachAlbum()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        Page<AlbumRow> page = await ListAsync(Query());

        page.Total.Should().Be(3);
        page.Items.Single(album => album.Id == library.FirstAlbum.Id).Should().BeEquivalentTo(new AlbumRow(
            library.FirstAlbum.Id,
            "First Album",
            library.Alpha.Id,
            "Alpha Band",
            false,
            2001,
            "Rock",
            3,
            TimeSpan.FromMinutes(9).TotalMilliseconds,
            true,
            library.FirstAlbum.FirstSeenAt,
            Artists: [new ArtistCreditRow(library.Alpha.Id, "Alpha Band")]));
        page.Items.Single(album => album.Id == library.SecondAlbum.Id).HasCover.Should().BeTrue();
        page.Items.Single(album => album.Id == library.Hits.Id).Should().BeEquivalentTo(new
        {
            ArtistId = (Guid?)null,
            ArtistName = (string?)null,
            IsCompilation = true,
            Genre = "Pop",
            TrackCount = 2,
            HasCover = false,
        });
    }

    [Test]
    public async Task ListAsync_GenreOfTheAlbum_IsTheFirstGenreByDiscAndTrackNumber()
    {
        LibrarySeed seed = new();
        Album album = seed.Album("Mixed");
        seed.Track("Third", album: album, number: 1, disc: 2, genre: "Jazz");
        seed.Track("Second", album: album, number: 2, disc: 1, genre: "Blues");
        seed.Track("First", album: album, number: 1, disc: 1);
        await SaveAsync(seed);

        Page<AlbumRow> page = await ListAsync(Query());

        page.Items.Single().Genre.Should().Be("Blues");
        page.Items.Single().DurationMilliseconds.Should().Be(0);
    }

    [Test]
    public async Task ListAsync_SeveralAlbumArtists_ListsTheAlbumForEachOfThemWithAllOfThem()
    {
        LibrarySeed seed = new();
        Artist first = seed.Artist("Bob Sinclar");
        Artist second = seed.Artist("K.Flay");
        Artist guest = seed.Artist("Guest");
        Album album = seed.Album("Together", first, moreAlbumArtists: [second]);
        seed.Track("Song", first, album, featuring: [guest]);
        await SaveAsync(seed);

        foreach (Artist artist in new[] { first, second, guest })
        {
            Page<AlbumRow> page = await ListAsync(Query(artistId: artist.Id));
            page.Items.Select(row => row.Id).Should().Equal(album.Id);
        }

        (await ListAsync(Query(search: "K.Flay"))).Items.Single().AllArtists.Should().Equal(
            new ArtistCreditRow(first.Id, "Bob Sinclar"),
            new ArtistCreditRow(second.Id, "K.Flay"));
    }

    [Test]
    public async Task ListAsync_ByArtist_PutsCompilationsLast()
    {
        await SeedStandardLibraryAsync();

        Page<AlbumRow> ascending = await ListAsync(Query());
        Page<AlbumRow> byTitle = await ListAsync(Query(sort: AlbumSortField.Title, direction: SortDirection.Desc));
        Page<AlbumRow> byYear = await ListAsync(Query(sort: AlbumSortField.Year));

        ascending.Items.Select(album => album.Title).Should().Equal("First Album", "Second Album", "Hits Collection");
        byTitle.Items.Select(album => album.Title).Should().Equal("Second Album", "Hits Collection", "First Album");
        byYear.Items.Select(album => album.Title).Should().Equal("First Album", "Hits Collection", "Second Album");
    }

    [Test]
    public async Task ListAsync_WithArtist_FindsAlbumsOfTheArtistAndAlbumsWithItsTracks()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        (await TitlesAsync(Query(artistId: library.Alpha.Id))).Should().Equal("First Album", "Hits Collection");
        (await TitlesAsync(Query(artistId: library.Guest.Id))).Should().Equal("First Album");
    }

    [Test]
    public async Task ListAsync_WithGenreYearAndSearch_ReturnsTheMatchingAlbums()
    {
        await SeedStandardLibraryAsync();

        (await TitlesAsync(Query(genre: "pop"))).Should().Equal("Second Album", "Hits Collection");
        (await TitlesAsync(Query(year: 2001))).Should().Equal("First Album");
        (await TitlesAsync(Query(search: "secnd album"))).Should().Contain("Second Album");
        (await TitlesAsync(Query(search: "beta"))).Should().Equal("Second Album");
    }

    [Test]
    public async Task ListAsync_ByRelevance_PutsTheBestMatchFirst()
    {
        await SeedStandardLibraryAsync();

        Page<AlbumRow> page = await ListAsync(
            Query(search: "First Album", sort: AlbumSortField.Relevance, direction: SortDirection.Desc));

        page.Items.First().Title.Should().Be("First Album");
    }

    [Test]
    public async Task FindAsync_KnownAndUnknownId_ReturnsTheAlbumOrNull()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        AlbumRepository repository = new(CreateDbContext());

        (await repository.FindAsync(library.Hits.Id))!.Title.Should().Be("Hits Collection");
        (await repository.FindAsync(Guid.NewGuid())).Should().BeNull();
        (await repository.ExistsAsync(library.Hits.Id)).Should().BeTrue();
        (await repository.ExistsAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Test]
    public async Task ListGroupKeysAsync_ByArtist_ReturnsAlbumArtistYearAndInitialOfTheArtistInListOrder()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        IReadOnlyList<GroupKeyRow> keys = await GroupKeysAsync(Query() with { Offset = 1, Limit = 1 });

        keys.Should().Equal(
            new GroupKeyRow(library.FirstAlbum.Id, library.Alpha.Id, 2001, "A"),
            new GroupKeyRow(library.SecondAlbum.Id, library.Beta.Id, 2010, "B"),
            new GroupKeyRow(library.Hits.Id, null, 2005, null));
    }

    [TestCase(AlbumSortField.Title, SortDirection.Asc, new[] { "F", "H", "S" })]
    [TestCase(AlbumSortField.Title, SortDirection.Desc, new[] { "S", "H", "F" })]
    [TestCase(AlbumSortField.Artist, SortDirection.Desc, new[] { null, "B", "A" })]
    [TestCase(AlbumSortField.Year, SortDirection.Asc, new string?[] { null, null, null })]
    [TestCase(AlbumSortField.FirstSeenAt, SortDirection.Desc, new string?[] { null, null, null })]
    public async Task ListGroupKeysAsync_BySortKey_TakesTheInitialOfItsText(
        AlbumSortField sort,
        SortDirection direction,
        string?[] initials)
    {
        await SeedStandardLibraryAsync();

        IReadOnlyList<GroupKeyRow> keys = await GroupKeysAsync(Query(sort, direction));

        keys.Select(key => key.Initial).Should().Equal(initials);
    }

    private static IEnumerable<TestCaseData> GroupKeyQueries()
    {
        foreach (AlbumSortField sort in Enum.GetValues<AlbumSortField>())
        {
            foreach (SortDirection direction in Enum.GetValues<SortDirection>())
            {
                yield return new TestCaseData(Query(sort, direction)).SetArgDisplayNames(
                    sort.ToString(),
                    direction.ToString());
            }
        }

        yield return new TestCaseData(Query(AlbumSortField.Relevance, SortDirection.Desc, search: "album"))
            .SetArgDisplayNames("Relevance", "search");
        yield return new TestCaseData(Query(genre: "pop")).SetArgDisplayNames("Artist", "genre");
        yield return new TestCaseData(Query(AlbumSortField.Year, year: 2005)).SetArgDisplayNames("Year", "year");
    }

    [TestCaseSource(nameof(GroupKeyQueries))]
    public async Task ListGroupKeysAsync_AnySortAndFilter_FollowsTheOrderOfTheList(AlbumListQuery query)
    {
        await SeedStandardLibraryAsync();

        IReadOnlyList<GroupKeyRow> keys = await GroupKeysAsync(query);
        Page<AlbumRow> list = await ListAsync(query);

        keys.Select(key => (key.AlbumId, key.ArtistId, key.Year)).Should().Equal(
            list.Items.Select(album => ((Guid?)album.Id, album.ArtistId, album.Year)));
    }

    [Test]
    public async Task ListGroupKeysAsync_WithArtist_FindsTheAlbumsOfTheArtistAndWithItsTracks()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        IReadOnlyList<GroupKeyRow> keys = await GroupKeysAsync(Query(artistId: library.Beta.Id));

        keys.Select(key => key.AlbumId).Should().Equal(library.SecondAlbum.Id, library.Hits.Id);
    }

    [Test]
    public async Task ListGroupKeysAsync_MyMusic_OnlyTheAlbumsWithTracksOfTheUser()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        LibrarySeed seed = new();
        User user = seed.User();
        seed.Favorite(user, library.Ballad);
        await SaveAsync(seed);

        IReadOnlyList<GroupKeyRow> keys = await GroupKeysAsync(Query() with { UserId = user.Id, MyMusic = true });

        keys.Select(key => key.AlbumId).Should().Equal(library.SecondAlbum.Id);
    }

    [Test]
    public async Task ListGroupAlbumsAsync_Always_ReturnsTitleArtistAndCoverOfTheExistingAlbums()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        IReadOnlyList<GroupAlbumRow> rows = await new AlbumRepository(CreateDbContext()).ListGroupAlbumsAsync(
            [library.FirstAlbum.Id, library.SecondAlbum.Id, library.Hits.Id, library.Hits.Id, Guid.NewGuid()]);

        // The cover of the second album is embedded in its track.
        rows.Should().BeEquivalentTo(
        [
            new GroupAlbumRow(
                library.FirstAlbum.Id,
                "First Album",
                library.Alpha.Id,
                "Alpha Band",
                false,
                true,
                [new ArtistCreditRow(library.Alpha.Id, "Alpha Band")]),
            new GroupAlbumRow(
                library.SecondAlbum.Id,
                "Second Album",
                library.Beta.Id,
                "Beta Singer",
                false,
                true,
                [new ArtistCreditRow(library.Beta.Id, "Beta Singer")]),
            new GroupAlbumRow(library.Hits.Id, "Hits Collection", null, null, true, false, []),
        ]);
    }

    [Test]
    public async Task FindAsync_AlbumWithoutOwnCover_HasACoverOnlyIfItMayShowARealPictureOfItsArtist()
    {
        LibrarySeed seed = new();
        Artist pictured = seed.Artist("Pictured");
        seed.ArtistPicture(pictured, ArtistPictureKind.Uploaded);
        Artist plain = seed.Artist("Plain");
        Album fallsBack = seed.Album("Falls Back", pictured);
        Album noFallback = seed.Album("No Fallback", pictured);
        noFallback.CoverFallback = AlbumCoverFallback.None;
        Album withoutPicture = seed.Album("Without Picture", plain);
        Album embedded = seed.Album("Embedded", plain);
        Track song = seed.Track("Song", plain, embedded, 1);
        seed.TrackCover(song, seed.File("Embedded/01 Song.mp3", MediaFileKind.Audio, song), CoverSource.Embedded);
        Album named = seed.Album("Named", plain);
        Track other = seed.Track("Other", plain, named, 1);
        seed.TrackCover(other, seed.File("Named/01 Other.jpg", MediaFileKind.Image, other));
        await SaveAsync(seed);
        AlbumRepository repository = new(CreateDbContext());

        // The collage of an artist without a picture is no picture of the artist, and a picture with the name of a
        // song is no cover of its album.
        (await repository.FindAsync(fallsBack.Id))!.HasCover.Should().BeTrue();
        (await repository.FindAsync(noFallback.Id))!.HasCover.Should().BeFalse();
        (await repository.FindAsync(withoutPicture.Id))!.HasCover.Should().BeFalse();
        (await repository.FindAsync(embedded.Id))!.HasCover.Should().BeTrue();
        (await repository.FindAsync(named.Id))!.HasCover.Should().BeFalse();
    }

    [Test]
    public async Task ListGroupAlbumsAsync_WithoutIds_ReturnsNothing()
    {
        IReadOnlyList<GroupAlbumRow> rows = await new AlbumRepository(CreateDbContext()).ListGroupAlbumsAsync([]);

        rows.Should().BeEmpty();
    }

    private static AlbumListQuery Query(
        AlbumSortField sort = AlbumSortField.Artist,
        SortDirection direction = SortDirection.Asc,
        string? search = null,
        Guid? artistId = null,
        string? genre = null,
        int? year = null)
    {
        return new AlbumListQuery(0, 100, sort, direction, search, artistId, genre, year);
    }

    private async Task<Page<AlbumRow>> ListAsync(AlbumListQuery query)
    {
        return await new AlbumRepository(CreateDbContext()).ListAsync(query);
    }

    private async Task<IReadOnlyList<GroupKeyRow>> GroupKeysAsync(AlbumListQuery query)
    {
        return await new AlbumRepository(CreateDbContext()).ListGroupKeysAsync(query);
    }

    private async Task<IReadOnlyList<string>> TitlesAsync(AlbumListQuery query)
    {
        return [.. (await ListAsync(query)).Items.Select(album => album.Title)];
    }
}
