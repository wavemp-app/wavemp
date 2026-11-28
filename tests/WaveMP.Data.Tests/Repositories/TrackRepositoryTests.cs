using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Data.Segments;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class TrackRepositoryTests : LibraryDatabaseTests
{
    protected override string DatabaseName => "wavemp_tests_data_library_tracks";

    [Test]
    public async Task ListAsync_ByAlbumArtist_KeepsAlbumsTogetherAndPutsCompilationsLast()
    {
        await SeedStandardLibraryAsync();

        Page<TrackRow> page = await ListAsync(
            new TrackListQuery(0, 100, TrackSortField.AlbumArtist, SortDirection.Asc));

        page.Items.Select(track => track.Title).Should().Equal(
            "Opening", "Closing", "Duet", "Ballad", "Clip", "Loose Song", "Hit One", "Hit Two");
        page.Total.Should().Be(8);
    }

    [Test]
    public async Task ListAsync_ByAlbum_SortsByAlbumTitleAndTrackNumber()
    {
        await SeedStandardLibraryAsync();

        Page<TrackRow> page = await ListAsync(new TrackListQuery(0, 100, TrackSortField.Album, SortDirection.Asc));

        page.Items.Select(track => track.Title).Should().Equal(
            "Opening", "Closing", "Duet", "Hit One", "Hit Two", "Ballad", "Clip", "Loose Song");
    }

    [Test]
    public async Task ListAsync_ByTitleDescending_ReversesTheTitles()
    {
        await SeedStandardLibraryAsync();

        Page<TrackRow> page = await ListAsync(new TrackListQuery(0, 100, TrackSortField.Title, SortDirection.Desc));

        page.Items.Select(track => track.Title).Should().Equal(
            "Opening", "Loose Song", "Hit Two", "Hit One", "Duet", "Closing", "Clip", "Ballad");
    }

    [Test]
    public async Task ListAsync_ByArtist_SortsByTheArtistOfTheTrack()
    {
        await SeedStandardLibraryAsync();

        Page<TrackRow> page = await ListAsync(new TrackListQuery(0, 100, TrackSortField.Artist, SortDirection.Asc));

        page.Items.Select(track => track.Title).Should().Equal(
            "Opening", "Closing", "Hit One", "Hit Two", "Ballad", "Clip", "Duet", "Loose Song");
    }

    [Test]
    public async Task ListAsync_ByFirstSeenAtDescending_ShowsTheNewestFirstAndKeepsAlbumsTogether()
    {
        await SeedStandardLibraryAsync();

        Page<TrackRow> page = await ListAsync(
            new TrackListQuery(0, 100, TrackSortField.FirstSeenAt, SortDirection.Desc));

        page.Items.Select(track => track.Title).Should().Equal(
            "Hit One", "Hit Two", "Clip", "Loose Song", "Ballad", "Opening", "Closing", "Duet");
    }

    [Test]
    public async Task ListAsync_ByDurationAndYear_SortsByTheKey()
    {
        await SeedStandardLibraryAsync();

        Page<TrackRow> byDuration = await ListAsync(
            new TrackListQuery(0, 100, TrackSortField.Duration, SortDirection.Desc, HasAudio: true));
        Page<TrackRow> byYear = await ListAsync(new TrackListQuery(0, 100, TrackSortField.Year, SortDirection.Asc));

        byDuration.Items.First().Title.Should().Be("Ballad");
        byYear.Items.Select(track => track.Title).Take(6).Should().Equal(
            "Opening", "Closing", "Duet", "Hit One", "Hit Two", "Ballad");
    }

    [Test]
    public async Task ListAsync_WithPages_ReturnsEveryTrackExactlyOnce()
    {
        await SeedStandardLibraryAsync();
        List<string> titles = [];

        for (int offset = 0; offset < 8; offset += 3)
        {
            Page<TrackRow> page = await ListAsync(
                new TrackListQuery(offset, 3, TrackSortField.Genre, SortDirection.Asc));
            page.Total.Should().Be(8);
            titles.AddRange(page.Items.Select(track => track.Title));
        }

        titles.Should().HaveCount(8).And.OnlyHaveUniqueItems();
    }

    [Test]
    public async Task ListAsync_WithArtist_FindsTracksOfTheArtistAndOnAlbumsOfTheArtist()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        Page<TrackRow> alpha = await ListAsync(Query(artistId: library.Alpha.Id));
        Page<TrackRow> guest = await ListAsync(Query(artistId: library.Guest.Id));

        alpha.Items.Select(track => track.Title).Should().Equal("Opening", "Closing", "Duet", "Hit One");
        guest.Items.Select(track => track.Title).Should().Equal("Duet", "Loose Song");
    }

    [Test]
    public async Task ListAsync_WithFilters_ReturnsTheMatchingTracks()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        (await TitlesAsync(Query(albumId: library.SecondAlbum.Id))).Should().Equal("Ballad");
        (await TitlesAsync(Query(genre: "ROCK"))).Should().Equal("Opening", "Closing", "Duet");
        (await TitlesAsync(Query(year: 2005))).Should().Equal("Hit One", "Hit Two");
        (await TitlesAsync(Query(hasVideo: true))).Should().Equal("Closing", "Clip");
        (await TitlesAsync(Query(hasAudio: false))).Should().Equal("Clip");
        (await TitlesAsync(Query(genre: "Ro%"))).Should().BeEmpty();
    }

    [TestCase("Closng", "Closing")]
    [TestCase("clos", "Closing")]
    [TestCase("balad", "Ballad")]
    [TestCase("opening", "Opening")]
    public async Task ListAsync_WithSearch_FindsTitlesDespiteTyposAndParts(string search, string expected)
    {
        await SeedStandardLibraryAsync();

        IReadOnlyList<string> titles = await TitlesAsync(Query(search: search));

        titles.Should().Contain(expected);
    }

    [Test]
    public async Task ListAsync_WithSearch_FindsTracksByArtistAndAlbum()
    {
        await SeedStandardLibraryAsync();

        (await TitlesAsync(Query(search: "Hits Colection"))).Should().BeEquivalentTo("Hit One", "Hit Two");
        (await TitlesAsync(Query(search: "Guest Star"))).Should().BeEquivalentTo("Duet", "Loose Song");
    }

    [Test]
    public async Task ListAsync_ByRelevance_PutsTheBestMatchFirst()
    {
        await SeedStandardLibraryAsync();

        Page<TrackRow> page = await ListAsync(
            new TrackListQuery(0, 100, TrackSortField.Relevance, SortDirection.Desc, Search: "Hit Two"));

        page.Items.First().Title.Should().Be("Hit Two");
    }

    [Test]
    public async Task ListAsync_WithWildcardsInTheSearch_MatchesThemLiterally()
    {
        LibrarySeed seed = new();
        seed.Track("Under_score");
        seed.Track("Plain");
        await SaveAsync(seed);

        (await TitlesAsync(Query(search: "_"))).Should().Equal("Under_score");
        (await TitlesAsync(Query(search: "%"))).Should().BeEmpty();
    }

    [Test]
    public async Task ListAsync_Always_ReturnsArtistAlbumAndCover()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        IReadOnlyList<TrackRow> tracks = (await ListAsync(Query())).Items;

        TrackRow duet = tracks.Single(track => track.Title == "Duet");
        duet.Should().BeEquivalentTo(new
        {
            ArtistId = library.Guest.Id,
            ArtistName = "Guest Star",
            AlbumId = library.FirstAlbum.Id,
            AlbumTitle = "First Album",
            AlbumArtistId = library.Alpha.Id,
            AlbumArtistName = "Alpha Band",
            AlbumIsCompilation = false,
            TrackNumber = 3,
            DiscNumber = 1,
            Year = 2001,
            Genre = "Rock",
            Duration = TimeSpan.FromMinutes(2),
            HasAudio = true,
            HasVideo = false,
            HasCover = true,
        });
        tracks.Single(track => track.Title == "Hit One").Should().BeEquivalentTo(new
        {
            AlbumArtistId = (Guid?)null,
            AlbumIsCompilation = true,
            HasCover = false,
        });
        tracks.Single(track => track.Title == "Ballad").HasCover.Should().BeTrue();
        tracks.Single(track => track.Title == "Clip").Should().BeEquivalentTo(new
        {
            AlbumId = (Guid?)null,
            HasAudio = false,
            HasVideo = true,
            HasCover = false,
        });
    }

    [Test]
    public async Task ListAsync_TrackOfAnAlbumWhoseOtherTrackHasACover_HasNoCover()
    {
        LibrarySeed seed = new();
        Album album = seed.Album("Album");
        Track first = seed.Track("First", album: album, number: 1);
        seed.Track("Second", album: album, number: 2);
        seed.TrackCover(first, seed.File("Album/01 First.jpg", MediaFileKind.Image, first));
        await SaveAsync(seed);

        IReadOnlyList<TrackRow> tracks = (await ListAsync(Query())).Items;

        tracks.Single(track => track.Title == "First").HasCover.Should().BeTrue();
        tracks.Single(track => track.Title == "Second").HasCover.Should().BeFalse();
    }

    [Test]
    public async Task FindAsync_KnownAndUnknownId_ReturnsTheTrackOrNull()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        TrackRepository repository = new(CreateDbContext());

        (await repository.FindAsync(library.Ballad.Id))!.Title.Should().Be("Ballad");
        (await repository.FindAsync(Guid.NewGuid())).Should().BeNull();
        (await repository.ExistsAsync(library.Clip.Id)).Should().BeTrue();
        (await repository.ExistsAsync(Guid.NewGuid())).Should().BeFalse();
    }

    [Test]
    public async Task ListFilesAsync_ByKind_ListsAudioVideoAndPicturesWithThePrimaryFileFirst()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        TrackRepository repository = new(CreateDbContext());

        Page<MediaFile> files = await repository.ListFilesAsync(
            new TrackFileListQuery(library.Closing.Id, 0, 100, MediaFileSortField.Kind, SortDirection.Asc));
        Page<MediaFile> videos = await repository.ListFilesAsync(new TrackFileListQuery(
            library.Closing.Id, 0, 100, MediaFileSortField.Kind, SortDirection.Asc, MediaFileKind.Video));
        Page<MediaFile> bySize = await repository.ListFilesAsync(
            new TrackFileListQuery(library.Closing.Id, 0, 100, MediaFileSortField.Size, SortDirection.Desc));

        files.Items.Select(file => file.FileName).Should().Equal(
            "02 - Closing.flac", "02 - Closing.mp3", "02 - Closing.mkv", "02 - Closing.jpg");
        videos.Items.Select(file => file.FileName).Should().Equal("02 - Closing.mkv");
        bySize.Items.First().FileName.Should().Be("02 - Closing.mkv");
    }

    [Test]
    public async Task ListGenresAsync_Always_CountsTheTracksAndAlbumsOfEachGenre()
    {
        await SeedStandardLibraryAsync();
        TrackRepository repository = new(CreateDbContext());

        Page<GenreRow> byName = await repository.ListGenresAsync(
            new GenreListQuery(0, 100, GenreSortField.Name, SortDirection.Asc));
        Page<GenreRow> byCount = await repository.ListGenresAsync(
            new GenreListQuery(0, 100, GenreSortField.TrackCount, SortDirection.Desc));
        Page<GenreRow> search = await repository.ListGenresAsync(
            new GenreListQuery(0, 100, GenreSortField.Name, SortDirection.Asc, "rok"));

        byName.Items.Select(genre => (genre.Name, genre.TrackCount, genre.AlbumCount))
            .Should().Equal(("Pop", 3, 2), ("Rock", 3, 1));
        byName.Total.Should().Be(2);
        byCount.Items.Select(genre => genre.Name).Should().Equal("Pop", "Rock");
        search.Items.Select(genre => genre.Name).Should().Equal("Rock");
    }

    [Test]
    public async Task ListYearsAsync_Always_CountsTracksAndAlbumsPerYear()
    {
        await SeedStandardLibraryAsync();
        TrackRepository repository = new(CreateDbContext());

        Page<YearRow> years = await repository.ListYearsAsync(new YearListQuery(0, 100, SortDirection.Desc));

        years.Items.Should().BeEquivalentTo(
            [new YearRow(2010, 1, 1), new YearRow(2005, 2, 1), new YearRow(2001, 3, 1)],
            options => options.WithStrictOrdering());
    }

    [Test]
    public async Task ListGenresAndYearsAsync_SourceFilter_CountOnlyTheTracksOfTheSource()
    {
        await SeedStandardLibraryAsync();
        LibrarySeed seed = new();
        MediaSource other = seed.Source();
        Artist artist = seed.Artist("Gamma Group");
        Album album = seed.Album("Other Album", artist);
        seed.Track("Elsewhere", artist, album, 1, null, 2001, "rock", source: other);
        seed.Track("Far Away", artist, album, 2, null, 1999, "Jazz", source: other);
        await SaveAsync(seed);
        TrackRepository repository = new(CreateDbContext());

        Page<GenreRow> genres = await repository.ListGenresAsync(
            new GenreListQuery(0, 100, GenreSortField.Name, SortDirection.Asc, SourceId: other.Id));
        Page<GenreRow> allGenres = await repository.ListGenresAsync(
            new GenreListQuery(0, 100, GenreSortField.Name, SortDirection.Asc));
        Page<YearRow> years = await repository.ListYearsAsync(
            new YearListQuery(0, 100, SortDirection.Desc, SourceId: other.Id));
        Page<YearRow> none = await repository.ListYearsAsync(
            new YearListQuery(0, 100, SortDirection.Desc, SourceId: Guid.NewGuid()));

        genres.Items.Select(genre => (genre.Name, genre.TrackCount, genre.AlbumCount))
            .Should().Equal(("Jazz", 1, 1), ("Rock", 1, 1));
        allGenres.Items.Select(genre => (genre.Name, genre.TrackCount, genre.AlbumCount))
            .Should().Equal(("Jazz", 1, 1), ("Pop", 3, 2), ("Rock", 4, 2));
        years.Items.Should().BeEquivalentTo(
            [new YearRow(2001, 1, 1), new YearRow(1999, 1, 1)],
            options => options.WithStrictOrdering());
        none.Total.Should().Be(0);
    }

    [Test]
    public async Task FindPrimaryFileAsync_WithDuplicates_ReturnsThePrimaryFileOfTheKind()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        TrackRepository repository = new(CreateDbContext());

        TrackFileRow? audio = await repository.FindPrimaryFileAsync(library.Closing.Id, MediaFileKind.Audio);
        TrackFileRow? video = await repository.FindPrimaryFileAsync(library.Closing.Id, MediaFileKind.Video);

        audio!.TrackId.Should().Be(library.Closing.Id);
        audio.File!.RelativePath.Should().Be("Alpha Band/First Album/02 - Closing.flac");
        video!.File!.RelativePath.Should().Be("Alpha Band/First Album/02 - Closing.mkv");
    }

    [Test]
    public async Task FindPrimaryFileAsync_TrackWithoutAudio_ReturnsTheTrackWithoutFile()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        TrackFileRow? row = await new TrackRepository(CreateDbContext())
            .FindPrimaryFileAsync(library.Clip.Id, MediaFileKind.Audio);

        row.Should().NotBeNull();
        row!.File.Should().BeNull();
    }

    [Test]
    public async Task FindPrimaryFileAsync_UnknownTrack_ReturnsNull()
    {
        await SeedStandardLibraryAsync();

        TrackFileRow? row = await new TrackRepository(CreateDbContext())
            .FindPrimaryFileAsync(Guid.NewGuid(), MediaFileKind.Audio);

        row.Should().BeNull();
    }

    [Test]
    public async Task FindPrimaryFileAsync_Always_ReturnsTheCorrectionsOfTheKind()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        TrackRepository repository = new(CreateDbContext());
        await repository.SetManualSongSegmentAsync(library.Closing.Id, MediaFileKind.Video, -1250, 60_000);
        await repository.SetManualSongSegmentAsync(library.Closing.Id, MediaFileKind.Audio, 300, null);

        TrackFileRow? video = await new TrackRepository(CreateDbContext())
            .FindPrimaryFileAsync(library.Closing.Id, MediaFileKind.Video);
        TrackFileRow? audio = await new TrackRepository(CreateDbContext())
            .FindPrimaryFileAsync(library.Closing.Id, MediaFileKind.Audio);

        (video!.ManualSongStartMilliseconds, video.ManualSongEndMilliseconds).Should().Be((-1250, 60_000));
        (audio!.ManualSongStartMilliseconds, audio.ManualSongEndMilliseconds).Should().Be((300, (int?)null));
        video.Segment.Should().Match<SongSegment>(segment => segment.Manual && segment.StartMilliseconds == -1250);
    }

    [Test]
    public async Task FindPrimaryFileAsync_WithoutCorrection_ReturnsTheWholeFile()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        TrackFileRow? row = await new TrackRepository(CreateDbContext())
            .FindPrimaryFileAsync(library.Closing.Id, MediaFileKind.Video);

        (row!.ManualSongStartMilliseconds, row.ManualSongEndMilliseconds).Should().Be(((int?)null, (int?)null));
        row.Segment.Should().Be(SongSegment.WholeFile);
    }

    [Test]
    public async Task SetManualSongSegmentAsync_KnownTrack_StoresItAndKeepsTheTimeOfTheLastChange()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        bool set = await new TrackRepository(CreateDbContext())
            .SetManualSongSegmentAsync(library.Closing.Id, MediaFileKind.Video, 1500, 200_000);

        set.Should().BeTrue();
        Track track = await CreateDbContext().Tracks.SingleAsync(track => track.Id == library.Closing.Id);
        (track.VideoSongStartMilliseconds, track.VideoSongEndMilliseconds).Should().Be((1500, 200_000));
        (track.AudioSongStartMilliseconds, track.AudioSongEndMilliseconds).Should().Be(((int?)null, (int?)null));
        track.UpdatedAt.Should().Be(library.Closing.UpdatedAt);
    }

    [Test]
    public async Task SetManualSongSegmentAsync_Nulls_RemovesTheCorrection()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        TrackRepository repository = new(CreateDbContext());
        await repository.SetManualSongSegmentAsync(library.Closing.Id, MediaFileKind.Audio, 1500, 200_000);

        await repository.SetManualSongSegmentAsync(library.Closing.Id, MediaFileKind.Audio, null, null);

        Track track = await CreateDbContext().Tracks.SingleAsync(track => track.Id == library.Closing.Id);
        (track.AudioSongStartMilliseconds, track.AudioSongEndMilliseconds).Should().Be(((int?)null, (int?)null));
    }

    [Test]
    public async Task SetManualSongSegmentAsync_UnknownTrack_ReturnsFalse()
    {
        await SeedStandardLibraryAsync();

        bool set = await new TrackRepository(CreateDbContext())
            .SetManualSongSegmentAsync(Guid.NewGuid(), MediaFileKind.Video, 1500, null);

        set.Should().BeFalse();
    }

    [Test]
    public async Task SetManualSongSegmentAsync_ChangedTrackSavedLater_KeepsTheCorrection()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        await using WaveMpDbContext scan = CreateDbContext();
        Track tracked = await scan.Tracks.SingleAsync(track => track.Id == library.Closing.Id);

        await new TrackRepository(CreateDbContext())
            .SetManualSongSegmentAsync(library.Closing.Id, MediaFileKind.Video, 800, null);
        tracked.Title = "Closing (Remastered)";
        await scan.SaveChangesAsync();

        Track track = await CreateDbContext().Tracks.SingleAsync(track => track.Id == library.Closing.Id);
        track.Title.Should().Be("Closing (Remastered)");
        track.VideoSongStartMilliseconds.Should().Be(800);
    }

    [Test]
    public async Task ListByIdsAsync_Always_ReturnsTheExistingTracksOnce()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        IReadOnlyList<TrackRow> rows = await new TrackRepository(CreateDbContext()).ListByIdsAsync(
            [library.Opening.Id, library.Ballad.Id, library.Opening.Id, Guid.NewGuid()]);

        rows.Select(row => row.Title).Should().BeEquivalentTo(["Opening", "Ballad"]);
        rows.Single(row => row.Title == "Ballad").HasCover.Should().BeTrue();
    }

    [Test]
    public async Task ListByIdsAsync_WithoutIds_ReturnsNothing()
    {
        IReadOnlyList<TrackRow> rows = await new TrackRepository(CreateDbContext()).ListByIdsAsync([]);

        rows.Should().BeEmpty();
    }

    [Test]
    public async Task ListGroupKeysAsync_ByAlbumArtist_ReturnsAlbumArtistYearAndInitialOfTheSortTextInListOrder()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        IReadOnlyList<GroupKeyRow> keys = await GroupKeysAsync(
            new TrackListQuery(0, 2, TrackSortField.AlbumArtist, SortDirection.Asc));

        // Without album the artist of the track counts; the compilation has no album artist.
        keys.Should().Equal(
            new GroupKeyRow(library.FirstAlbum.Id, library.Alpha.Id, 2001, "A"),
            new GroupKeyRow(library.FirstAlbum.Id, library.Alpha.Id, 2001, "A"),
            new GroupKeyRow(library.FirstAlbum.Id, library.Guest.Id, 2001, "A"),
            new GroupKeyRow(library.SecondAlbum.Id, library.Beta.Id, 2010, "B"),
            new GroupKeyRow(null, library.Beta.Id, null, "B"),
            new GroupKeyRow(null, library.Guest.Id, null, "G"),
            new GroupKeyRow(library.Hits.Id, library.Alpha.Id, 2005, null),
            new GroupKeyRow(library.Hits.Id, library.Beta.Id, 2005, null));
    }

    [TestCase(TrackSortField.Album, SortDirection.Asc, new[] { "F", "F", "F", "H", "H", "S", null, null })]
    [TestCase(TrackSortField.Title, SortDirection.Desc, new[] { "O", "L", "H", "H", "D", "C", "C", "B" })]
    [TestCase(TrackSortField.Artist, SortDirection.Asc, new[] { "A", "A", "A", "B", "B", "B", "G", "G" })]
    [TestCase(TrackSortField.Genre, SortDirection.Asc, new[] { "P", "P", "P", "R", "R", "R", null, null })]
    [TestCase(TrackSortField.Year, SortDirection.Asc, new string?[] { null, null, null, null, null, null, null, null })]
    [TestCase(
        TrackSortField.Duration,
        SortDirection.Desc,
        new string?[] { null, null, null, null, null, null, null, null })]
    public async Task ListGroupKeysAsync_BySortKey_TakesTheInitialOfItsText(
        TrackSortField sort,
        SortDirection direction,
        string?[] initials)
    {
        await SeedStandardLibraryAsync();

        IReadOnlyList<GroupKeyRow> keys = await GroupKeysAsync(new TrackListQuery(0, 100, sort, direction));

        // The genres "Rock" and "rock" are one key whose order of spellings depends on the collation.
        keys.Select(key => key.Initial?.ToUpperInvariant()).Should().Equal(initials);
    }

    private static IEnumerable<TestCaseData> GroupKeyQueries()
    {
        foreach (TrackSortField sort in Enum.GetValues<TrackSortField>().Where(sort => sort != TrackSortField.Rating))
        {
            foreach (SortDirection direction in Enum.GetValues<SortDirection>())
            {
                yield return new TestCaseData(sort, direction, null, null).SetArgDisplayNames(
                    sort.ToString(),
                    direction.ToString());
            }
        }

        yield return new TestCaseData(TrackSortField.Relevance, SortDirection.Desc, "hit", null)
            .SetArgDisplayNames("Relevance", "search");
        yield return new TestCaseData(TrackSortField.AlbumArtist, SortDirection.Asc, null, "rock")
            .SetArgDisplayNames("AlbumArtist", "genre");
    }

    [TestCaseSource(nameof(GroupKeyQueries))]
    public async Task ListGroupKeysAsync_AnySortAndFilter_FollowsTheOrderOfTheList(
        TrackSortField sort,
        SortDirection direction,
        string? search,
        string? genre)
    {
        await SeedStandardLibraryAsync();
        TrackListQuery query = new(0, 100, sort, direction, search, Genre: genre);

        IReadOnlyList<GroupKeyRow> keys = await GroupKeysAsync(query);
        Page<TrackRow> list = await ListAsync(query);

        keys.Select(key => (key.AlbumId, key.ArtistId, key.Year)).Should().Equal(
            list.Items.Select(track => (track.AlbumId, track.ArtistId, track.Year)));
        keys.Should().HaveCount(list.Total);
    }

    [Test]
    public async Task ListGroupKeysAsync_MyMusicSortedByRating_FollowsTheOrderOfTheList()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        LibrarySeed seed = new();
        User user = seed.User();
        seed.Rating(user, library.Ballad, 5);
        seed.Rating(user, library.Opening, 3);
        seed.Favorite(user, library.Hits);
        await SaveAsync(seed);
        TrackListQuery query = new(
            0,
            100,
            TrackSortField.Rating,
            SortDirection.Desc,
            UserId: user.Id,
            MyMusic: true);

        IReadOnlyList<GroupKeyRow> keys = await GroupKeysAsync(query);

        keys.Select(key => key.AlbumId).Should().Equal(
            library.SecondAlbum.Id,
            library.FirstAlbum.Id,
            library.Hits.Id,
            library.Hits.Id);
    }

    [Test]
    public async Task ListGroupKeysAsync_WithArtistAndPaging_IgnoresThePagingButNotTheFilter()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        IReadOnlyList<GroupKeyRow> keys = await GroupKeysAsync(
            new TrackListQuery(2, 1, TrackSortField.AlbumArtist, SortDirection.Asc, ArtistId: library.Alpha.Id));

        keys.Select(key => key.AlbumId).Should().Equal(
            library.FirstAlbum.Id,
            library.FirstAlbum.Id,
            library.FirstAlbum.Id,
            library.Hits.Id);
    }

    [Test]
    public async Task ListAsync_SongWithSeveralArtists_IsListedForEachOfThemWithAllItsArtists()
    {
        LibrarySeed seed = new();
        Artist bob = seed.Artist("Bob Sinclar");
        Artist simon = seed.Artist("Simon & Garfunkel");
        Artist kflay = seed.Artist("K.Flay");
        Artist other = seed.Artist("Other");
        Track song = seed.Track("Together", bob, moreArtists: [simon], featuring: [kflay]);
        seed.Track("Alone", other);
        await SaveAsync(seed);

        foreach (Artist artist in new[] { bob, simon, kflay })
        {
            Page<TrackRow> page = await ListAsync(Query(artistId: artist.Id));
            page.Items.Select(track => track.Id).Should().Equal(song.Id);
        }

        TrackRow row = (await ListAsync(Query(search: "K.Flay"))).Items.Single();
        row.Id.Should().Be(song.Id);
        (row.ArtistId, row.ArtistName).Should().Be((bob.Id, "Bob Sinclar"));
        row.AllArtists.Should().Equal(
            new ArtistCreditRow(bob.Id, "Bob Sinclar"),
            new ArtistCreditRow(simon.Id, "Simon & Garfunkel"),
            new ArtistCreditRow(kflay.Id, "K.Flay", ArtistRole.Featured));
    }

    [Test]
    public async Task ListAsync_AlbumWithSeveralAlbumArtists_ListsItsSongsForEachOfThem()
    {
        LibrarySeed seed = new();
        Artist first = seed.Artist("First");
        Artist second = seed.Artist("Second");
        Album album = seed.Album("Shared", first, moreAlbumArtists: [second]);
        Track song = seed.Track("Song", first, album);
        await SaveAsync(seed);

        TrackRow row = (await ListAsync(Query(artistId: second.Id))).Items.Single();

        row.Id.Should().Be(song.Id);
        row.AllAlbumArtists.Select(artist => artist.Name).Should().Equal("First", "Second");
    }

    private static TrackListQuery Query(
        string? search = null,
        Guid? artistId = null,
        Guid? albumId = null,
        string? genre = null,
        int? year = null,
        bool? hasAudio = null,
        bool? hasVideo = null)
    {
        return new TrackListQuery(
            0,
            100,
            TrackSortField.AlbumArtist,
            SortDirection.Asc,
            search,
            artistId,
            albumId,
            genre,
            year,
            hasAudio,
            hasVideo);
    }

    private async Task<Page<TrackRow>> ListAsync(TrackListQuery query)
    {
        return await new TrackRepository(CreateDbContext()).ListAsync(query);
    }

    private async Task<IReadOnlyList<GroupKeyRow>> GroupKeysAsync(TrackListQuery query)
    {
        return await new TrackRepository(CreateDbContext()).ListGroupKeysAsync(query);
    }

    private async Task<IReadOnlyList<string>> TitlesAsync(TrackListQuery query)
    {
        return [.. (await ListAsync(query)).Items.Select(track => track.Title)];
    }
}
