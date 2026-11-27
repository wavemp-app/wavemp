using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

/// <summary>
/// The tolerant search of the lists of artists, albums and tracks: aliases, symbol variants and similar names, and the
/// order of the matches.
/// </summary>
[TestFixture]
[Category("Integration")]
public class LibrarySearchTests : LibraryDatabaseTests
{
    protected override string DatabaseName => "wavemp_tests_data_library_search";

    [TestCase("pink", "P!nk")]
    [TestCase("p!nk", "Pink Floyd")]
    [TestCase("kesha", "Ke$ha")]
    [TestCase("KE$HA", "Ke$ha")]
    [TestCase("rihana", "Rihanna")]
    [TestCase("rihanna", "Rihanna")]
    [TestCase("simon and garfunkel", "Simon & Garfunkel")]
    [TestCase("simon garfunkel", "Simon & Garfunkel")]
    [TestCase("gorillaz", "G0rillaz")]
    [TestCase("alecia moore", "P!nk")]
    [TestCase("alecia mor", "P!nk")]
    public async Task ArtistList_TolerantSearch_FindsTheArtist(string search, string expected)
    {
        await SeedArtistsAsync();

        Page<ArtistRow> page = await ListArtistsAsync(search);

        page.Items.Select(artist => artist.Name).Should().Contain(expected);
    }

    [Test]
    public async Task ArtistList_SearchByAlias_ShowsTheNameOfTheArtist()
    {
        await SeedArtistsAsync();

        Page<ArtistRow> page = await ListArtistsAsync("Alecia Moore");

        page.Items.Should().ContainSingle().Which.Name.Should().Be("P!nk");
    }

    [Test]
    public async Task ArtistList_RemovedAlias_IsNotSearched()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("Band");
        seed.Alias(artist, "Removed Name", ArtistAliasSource.MusicBrainz, isRemoved: true);
        seed.Track("Song", artist);
        await SaveAsync(seed);

        Page<ArtistRow> page = await ListArtistsAsync("Removed Name");

        page.Items.Should().BeEmpty();
    }

    [Test]
    public async Task ArtistList_Relevance_ExactThenStartThenContainsThenAliasThenSimilar()
    {
        LibrarySeed seed = new();
        Artist exact = seed.Artist("P!nk");
        Artist start = seed.Artist("Pink Floyd");
        Artist contains = seed.Artist("The Pink Panthers");
        Artist alias = seed.Artist("Alecia");
        seed.Alias(alias, "Pinkie");
        Artist similar = seed.Artist("Pinc");
        seed.Artist("Unrelated");
        foreach (Artist artist in new[] { exact, start, contains, alias, similar })
        {
            seed.Track("Song of " + artist.Name, artist);
        }

        await SaveAsync(seed);

        Page<ArtistRow> page = await ListArtistsAsync("pink");

        page.Items.Select(artist => artist.Name).Should().Equal(
            "P!nk",
            "Pink Floyd",
            "The Pink Panthers",
            "Alecia",
            "Pinc");
    }

    [Test]
    public async Task AlbumList_TolerantSearch_FindsByTitleAndByAliasOfTheArtist()
    {
        LibrarySeed seed = new();
        Artist pink = seed.Artist("P!nk");
        seed.Alias(pink, "Alecia Moore");
        Album album = seed.Album("Funhouse", pink);
        seed.Track("So What", pink, album);
        Artist other = seed.Artist("Other");
        Album rock = seed.Album("Rock & Roll", other);
        seed.Track("Song", other, rock);
        await SaveAsync(seed);

        (await ListAlbumsAsync("alecia moore")).Items.Select(row => row.Title).Should().Equal("Funhouse");
        (await ListAlbumsAsync("pink")).Items.Select(row => row.Title).Should().Equal("Funhouse");
        (await ListAlbumsAsync("rock and roll")).Items.Select(row => row.Title).Should().Equal("Rock & Roll");
        (await ListAlbumsAsync("funhose")).Items.Select(row => row.Title).Should().Equal("Funhouse");
    }

    [Test]
    public async Task AlbumList_Relevance_TitleBeforeAlbumsOfTheArtist()
    {
        LibrarySeed seed = new();
        Artist pink = seed.Artist("P!nk");
        Album funhouse = seed.Album("Funhouse", pink);
        seed.Track("So What", pink, funhouse);
        Artist band = seed.Artist("Band");
        Album named = seed.Album("Pink", band);
        seed.Track("Song", band, named);
        await SaveAsync(seed);

        Page<AlbumRow> page = await ListAlbumsAsync("pink");

        page.Items.Select(row => row.Title).Should().Equal("Pink", "Funhouse");
    }

    [Test]
    public async Task TrackList_TolerantSearch_FindsByTitleArtistAliasAndAlbum()
    {
        LibrarySeed seed = new();
        Artist kesha = seed.Artist("Ke$ha");
        seed.Alias(kesha, "Kesha Rose Sebert");
        Album animal = seed.Album("Animal", kesha);
        seed.Track("Tik Tok", kesha, animal);
        Artist other = seed.Artist("Other");
        seed.Track("L0ve Song", other);
        await SaveAsync(seed);

        (await ListTracksAsync("kesha")).Items.Select(row => row.Title).Should().Equal("Tik Tok");
        (await ListTracksAsync("sebert")).Items.Select(row => row.Title).Should().Equal("Tik Tok");
        (await ListTracksAsync("animal")).Items.Select(row => row.Title).Should().Equal("Tik Tok");
        (await ListTracksAsync("love song")).Items.Select(row => row.Title).Should().Equal("L0ve Song");
        (await ListTracksAsync("tik tock")).Items.Select(row => row.Title).Should().Equal("Tik Tok");
    }

    [Test]
    public async Task SaveChanges_NewAndRenamedEntries_GetTheirSearchKeys()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("P!nk");
        Album album = seed.Album("Rock & Roll", artist);
        Track track = seed.Track("Ke$ha Song", artist, album);
        ArtistAlias alias = seed.Alias(artist, "B3ck");
        await SaveAsync(seed);

        await using (WaveMpDbContext dbContext = CreateDbContext())
        {
            Track stored = await dbContext.Tracks.SingleAsync(row => row.Id == track.Id);
            stored.Title = "G0rillaz Song";
            await dbContext.SaveChangesAsync();
        }

        await using WaveMpDbContext read = CreateDbContext();
        (await read.Artists.SingleAsync(row => row.Id == artist.Id)).SearchKey.Should().Be("pnk pink");
        (await read.Albums.SingleAsync(row => row.Id == album.Id)).SearchKey
            .Should().Be("rockroll rockandroll");
        (await read.Tracks.SingleAsync(row => row.Id == track.Id)).SearchKey
            .Should().Be("g0rillazsong gorillazsong");
        (await read.ArtistAliases.SingleAsync(row => row.Id == alias.Id)).SearchKey.Should().Be("b3ck beck");
    }

    [Test]
    public async Task SearchKeyUpgrade_Pending_SetsTheKeysOfExistingEntriesOnce()
    {
        LibrarySeed seed = new();
        Artist artist = seed.Artist("P!nk");
        Album album = seed.Album("Funhouse", artist);
        seed.Track("So What", artist, album);
        seed.Alias(artist, "Alecia Moore");
        await SaveAsync(seed);
        await using (WaveMpDbContext dbContext = CreateDbContext())
        {
            // Like rows written before the migration that added the keys.
            await dbContext.Database.ExecuteSqlRawAsync(
                """
                UPDATE artists SET search_key = '';
                UPDATE albums SET search_key = '';
                UPDATE tracks SET search_key = '';
                UPDATE artist_aliases SET search_key = '';
                DELETE FROM data_upgrades WHERE name = 'search-keys';
                INSERT INTO data_upgrades (name, completed_at) VALUES ('search-keys', NULL);
                """);
        }

        SearchKeyUpgradeRepository repository = new(CreateDbContext());
        (await repository.IsPendingAsync()).Should().BeTrue();

        int count = await repository.ApplyAsync(new DateTimeOffset(2026, 11, 27, 12, 0, 0, TimeSpan.Zero));

        count.Should().Be(4);
        (await repository.IsPendingAsync()).Should().BeFalse();
        (await ListArtistsAsync("pink")).Items.Should().ContainSingle();
        await using WaveMpDbContext read = CreateDbContext();
        (await read.Tracks.SingleAsync()).SearchKey.Should().Be("sowhat");
        (await read.ArtistAliases.SingleAsync()).SearchKey.Should().Be("aleciamoore");
    }

    private async Task SeedArtistsAsync()
    {
        LibrarySeed seed = new();
        Artist pink = seed.Artist("P!nk");
        seed.Alias(pink, "Alecia Moore");
        foreach (Artist artist in new[]
        {
            pink,
            seed.Artist("Pink Floyd"),
            seed.Artist("Ke$ha"),
            seed.Artist("Rihanna"),
            seed.Artist("Simon & Garfunkel"),
            seed.Artist("G0rillaz"),
            seed.Artist("Muse"),
        })
        {
            seed.Track("Song", artist);
        }

        await SaveAsync(seed);
    }

    private Task<Page<ArtistRow>> ListArtistsAsync(string search)
    {
        return new ArtistRepository(CreateDbContext()).ListAsync(
            new ArtistListQuery(0, 100, ArtistSortField.Relevance, SortDirection.Desc, search));
    }

    private Task<Page<AlbumRow>> ListAlbumsAsync(string search)
    {
        return new AlbumRepository(CreateDbContext()).ListAsync(
            new AlbumListQuery(0, 100, AlbumSortField.Relevance, SortDirection.Desc, search));
    }

    private Task<Page<TrackRow>> ListTracksAsync(string search)
    {
        return new TrackRepository(CreateDbContext()).ListAsync(
            new TrackListQuery(0, 100, TrackSortField.Relevance, SortDirection.Desc, search));
    }
}
