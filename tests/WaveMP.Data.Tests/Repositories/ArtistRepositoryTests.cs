using AwesomeAssertions;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class ArtistRepositoryTests : LibraryDatabaseTests
{
    protected override string DatabaseName => "wavemp_tests_library_artists";

    [Test]
    public async Task ListAsync_ByName_CountsAlbumsAndTracksOfEachArtist()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        Page<ArtistRow> page = await ListAsync(new ArtistListQuery(0, 100, ArtistSortField.Name, SortDirection.Asc));

        // The pictures have tests of their own.
        page.Items.Select(artist => artist with { Picture = null }).Should().BeEquivalentTo(
            [
                new ArtistRow(library.Alpha.Id, "Alpha Band", 2, 4, library.Alpha.FirstSeenAt),
                new ArtistRow(library.Beta.Id, "Beta Singer", 2, 3, library.Beta.FirstSeenAt),
                new ArtistRow(library.Guest.Id, "Guest Star", 1, 2, library.Guest.FirstSeenAt),
            ],
            options => options.WithStrictOrdering());
    }

    [Test]
    public async Task ListAsync_ByFirstSeenAtDescending_ShowsTheNewestFirst()
    {
        await SeedStandardLibraryAsync();

        Page<ArtistRow> page = await ListAsync(
            new ArtistListQuery(0, 100, ArtistSortField.FirstSeenAt, SortDirection.Desc));

        page.Items.Select(artist => artist.Name).Should().Equal("Beta Singer", "Alpha Band", "Guest Star");
    }

    [TestCase("alpa band", "Alpha Band")]
    [TestCase("SINGER", "Beta Singer")]
    [TestCase("gest star", "Guest Star")]
    public async Task ListAsync_WithSearch_FindsTheArtistDespiteTypos(string search, string expected)
    {
        await SeedStandardLibraryAsync();

        Page<ArtistRow> page = await ListAsync(
            new ArtistListQuery(0, 100, ArtistSortField.Relevance, SortDirection.Desc, search));

        page.Items.First().Name.Should().Be(expected);
    }

    [Test]
    public async Task FindAsync_KnownAndUnknownId_ReturnsTheArtistOrNull()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        ArtistRepository repository = new(CreateDbContext());

        (await repository.FindAsync(library.Guest.Id))!.TrackCount.Should().Be(2);
        (await repository.FindAsync(Guid.NewGuid())).Should().BeNull();
        (await repository.ExistsAsync(library.Beta.Id)).Should().BeTrue();
        (await repository.ExistsAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Test]
    public async Task ListAsync_ArtistsWithoutPicture_NameTheAlbumsWithACoverForTheCollage()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        Page<ArtistRow> page = await ListAsync(new ArtistListQuery(0, 100, ArtistSortField.Name, SortDirection.Asc));

        // Alpha Band: its own album with a cover, not the compilation without one; Beta Singer: the album whose track
        // has an embedded cover; Guest Star: the album with its duet.
        Guid[][] expected = [[library.FirstAlbum.Id], [library.SecondAlbum.Id], [library.FirstAlbum.Id]];
        page.Items.Select(artist => artist.Picture!.CollageAlbumIds)
            .Should().BeEquivalentTo(expected, options => options.WithStrictOrdering());
        page.Items.Should().AllSatisfy(artist => artist.Picture.Should().BeEquivalentTo(new
        {
            LocalFileId = (Guid?)null,
            UploadedPictureId = (Guid?)null,
            OnlinePicture = (ArtistOnlinePictureRow?)null,
            HasOwnPicture = false,
        }));
    }

    [Test]
    public async Task FindAsync_ManyAlbums_NamesFourForTheCollageAlbumArtistFirstThenNewestFirst()
    {
        LibrarySeed seed = new();
        Artist band = seed.Artist("Collage Band");
        Artist other = seed.Artist("Other Band");
        Album older = AlbumWithCover(seed, "Older", band, 2001);
        Album undated = AlbumWithCover(seed, "Undated", band, null);
        Album newerB = AlbumWithCover(seed, "Bright", band, 2010);
        Album newerA = AlbumWithCover(seed, "Afternoon", band, 2010);
        Album guestAppearance = AlbumWithCover(seed, "Guest Appearance", other, 2024);
        seed.Track("Feature", band, guestAppearance, 2);
        Album withoutCover = seed.Album("Without Cover", band, year: 2030);
        seed.Track("Plain", band, withoutCover, 1);
        await SaveAsync(seed);

        ArtistRow? artist = await new ArtistRepository(CreateDbContext()).FindAsync(band.Id);

        artist!.Picture!.CollageAlbumIds.Should().Equal(newerA.Id, newerB.Id, older.Id, undated.Id);
        ArtistRow? guest = await new ArtistRepository(CreateDbContext()).FindAsync(other.Id);
        guest!.Picture!.CollageAlbumIds.Should().Equal(guestAppearance.Id);
    }

    [Test]
    public async Task FindAsync_FewAlbumArtistAlbums_AddsTheAlbumsWithItsTracksAfterThem()
    {
        LibrarySeed seed = new();
        Artist singer = seed.Artist("Solo Singer");
        Artist host = seed.Artist("Host");
        Album own = AlbumWithCover(seed, "Own Record", singer, 1999);
        Album hosted = AlbumWithCover(seed, "Hosted Show", host, 2025);
        seed.Track("Guest Spot", singer, hosted, 3);
        Album compilation = seed.Album("Hits", isCompilation: true, year: 2020);
        Track hit = seed.Track("Hit", singer, compilation, 1);
        seed.TrackCover(
            hit,
            seed.File("Hits/01.mp3", MediaFileKind.Audio, hit, hasEmbeddedPicture: true),
            CoverSource.Embedded);
        // A picture with the name of a song is no cover of its album, so this album has none for the collage.
        Album singles = seed.Album("Singles", year: 2021);
        Track single = seed.Track("Single", singer, singles, 1);
        seed.TrackCover(single, seed.File("Singles/01 Single.jpg", MediaFileKind.Image, single));
        await SaveAsync(seed);

        IReadOnlyList<ArtistRow> artists =
            await new ArtistRepository(CreateDbContext()).ListByIdsAsync([singer.Id]);

        artists.Single().Picture!.CollageAlbumIds.Should().Equal(own.Id, hosted.Id, compilation.Id);
    }

    [Test]
    public async Task FindAsync_PicturesOfAllKinds_NamesThemAndLeavesOutTheCollage()
    {
        LibrarySeed seed = new();
        Artist band = seed.Artist("Pictured Band");
        AlbumWithCover(seed, "Record", band, 2020);
        MediaFile local = seed.File("Pictured Band/artist.jpg", MediaFileKind.Image, isPrimary: false);
        band.LocalPictureFileId = local.Id;
        ArtistPicture uploaded = seed.ArtistPicture(band, ArtistPictureKind.Uploaded);
        ArtistPicture online = seed.ArtistPicture(band, ArtistPictureKind.Online);
        band.MusicBrainzArtistId = Guid.Parse("5b11f4ce-a62d-471e-81fc-a69a8278c7da");
        await SaveAsync(seed);

        ArtistRow? artist = await new ArtistRepository(CreateDbContext()).FindAsync(band.Id);

        artist!.Picture.Should().BeEquivalentTo(new
        {
            LocalFileId = local.Id,
            LocalModifiedAt = local.ModifiedAt,
            UploadedPictureId = uploaded.Id,
            OnlinePicture = new ArtistOnlinePictureRow(
                online.Id,
                ArtistPictureProvider.Wikimedia,
                "Jane Doe",
                "CC BY-SA 4.0",
                "https://creativecommons.org/licenses/by-sa/4.0/",
                "https://commons.wikimedia.org/wiki/File:Example.jpg"),
            band.MusicBrainzArtistId,
            CollageAlbumIds = Array.Empty<Guid>(),
            HasOwnPicture = true,
        });
    }

    [Test]
    public async Task FindAsync_OnlyAnOnlinePicture_HasItsOwnPicture()
    {
        LibrarySeed seed = new();
        Artist band = seed.Artist("Online Band");
        AlbumWithCover(seed, "Record", band, 2020);
        ArtistPicture online = seed.ArtistPicture(band, ArtistPictureKind.Online);
        await SaveAsync(seed);

        ArtistRow? artist = await new ArtistRepository(CreateDbContext()).FindAsync(band.Id);

        artist!.Picture!.OnlinePicture!.Id.Should().Be(online.Id);
        artist.Picture.UploadedPictureId.Should().BeNull();
        artist.Picture.CollageAlbumIds.Should().BeEmpty();
    }

    [TestCase(SortDirection.Asc, new[] { "A", "B", "G" })]
    [TestCase(SortDirection.Desc, new[] { "G", "B", "A" })]
    public async Task ListGroupKeysAsync_ByName_ReturnsTheArtistsWithTheInitialOfTheirNameInListOrder(
        SortDirection direction,
        string[] initials)
    {
        await SeedStandardLibraryAsync();
        ArtistListQuery query = new(1, 1, ArtistSortField.Name, direction);

        IReadOnlyList<GroupKeyRow> keys = await GroupKeysAsync(query);
        Page<ArtistRow> list = await ListAsync(query with { Offset = 0, Limit = 100 });

        keys.Select(key => key.Initial).Should().Equal(initials);
        keys.Select(key => key.ArtistId).Should().Equal(list.Items.Select(artist => (Guid?)artist.Id));
        keys.Should().OnlyContain(key => key.AlbumId == null && key.Year == null);
    }

    [TestCase(ArtistSortField.FirstSeenAt, SortDirection.Desc, null)]
    [TestCase(ArtistSortField.FirstSeenAt, SortDirection.Asc, null)]
    [TestCase(ArtistSortField.Relevance, SortDirection.Desc, "band")]
    public async Task ListGroupKeysAsync_OtherSort_FollowsTheOrderOfTheListWithoutInitials(
        ArtistSortField sort,
        SortDirection direction,
        string? search)
    {
        await SeedStandardLibraryAsync();
        ArtistListQuery query = new(0, 100, sort, direction, search);

        IReadOnlyList<GroupKeyRow> keys = await GroupKeysAsync(query);
        Page<ArtistRow> list = await ListAsync(query);

        keys.Select(key => key.ArtistId).Should().Equal(list.Items.Select(artist => (Guid?)artist.Id));
        keys.Should().OnlyContain(key => key.Initial == null);
    }

    [Test]
    public async Task ListGroupKeysAsync_MyMusicAndSource_FilterLikeTheList()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        LibrarySeed seed = new();
        User user = seed.User();
        seed.Favorite(user, library.Duet);
        MediaSource source = seed.Source();
        Artist other = seed.Artist("Other Source Artist");
        seed.Track("Elsewhere", other, source: source);
        await SaveAsync(seed);

        IReadOnlyList<GroupKeyRow> mine = await GroupKeysAsync(
            new ArtistListQuery(0, 100, ArtistSortField.Name, SortDirection.Asc, UserId: user.Id, MyMusic: true));
        IReadOnlyList<GroupKeyRow> ofSource = await GroupKeysAsync(
            new ArtistListQuery(0, 100, ArtistSortField.Name, SortDirection.Asc, SourceId: source.Id));

        // The duet is by the guest on an album of the band.
        mine.Select(key => key.ArtistId).Should().Equal(library.Alpha.Id, library.Guest.Id);
        ofSource.Should().Equal(new GroupKeyRow(null, other.Id, null, "O"));
    }

    [Test]
    public async Task ListGroupArtistsAsync_Always_ReturnsTheNamesOfTheExistingArtists()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        IReadOnlyList<GroupArtistRow> rows = await new ArtistRepository(CreateDbContext()).ListGroupArtistsAsync(
            [library.Alpha.Id, library.Guest.Id, library.Alpha.Id, Guid.NewGuid()]);
        IReadOnlyList<GroupArtistRow> none = await new ArtistRepository(CreateDbContext()).ListGroupArtistsAsync([]);

        rows.Should().BeEquivalentTo(
            [new GroupArtistRow(library.Alpha.Id, "Alpha Band"), new GroupArtistRow(library.Guest.Id, "Guest Star")]);
        none.Should().BeEmpty();
    }

    private async Task<IReadOnlyList<GroupKeyRow>> GroupKeysAsync(ArtistListQuery query)
    {
        return await new ArtistRepository(CreateDbContext()).ListGroupKeysAsync(query);
    }

    private static Album AlbumWithCover(LibrarySeed seed, string title, Artist artist, int? year)
    {
        Album album = seed.Album(title, artist, year: year);
        seed.Track(title + " Song", artist, album, 1);
        seed.AlbumCover(album, seed.File($"{artist.Name}/{title}/cover.jpg", MediaFileKind.Image, isPrimary: false));
        return album;
    }

    private async Task<Page<ArtistRow>> ListAsync(ArtistListQuery query)
    {
        return await new ArtistRepository(CreateDbContext()).ListAsync(query);
    }
}
