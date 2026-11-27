using System.Security.Cryptography;
using System.Text;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WaveMP.Core.Scanning.Extensions;
using WaveMP.Core.Scanning.Upgrades;
using WaveMP.Data;
using WaveMP.Data.Entities;
using WaveMP.Data.Extensions;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Databases;

namespace WaveMP.Core.Scanning.Tests.Upgrades;

/// <summary>
/// The migration to ids and normalized names with the upgrade that the service runs after it, on a realistic library
/// stored before the migration (see NameKeyUpgradeSeed.sql): entries whose normalized names are equal are joined, and
/// no favorite, rating, play, playlist entry, cover rule, picture or player is lost.
/// </summary>
[TestFixture]
[Category("Integration")]
public class NameKeyUpgraderDatabaseTests
{
    private const string DatabaseName = "wavemp_tests_scanning_name_key_upgrade";
    private const string PreviousMigration = "20261126093256_AddAlbumCoverFallback";

    private TestDatabase _database = null!;
    private ServiceProvider _services = null!;

    [SetUp]
    public async Task SetUp()
    {
        _database = await TestDatabase.CreateMigratedAsync(DatabaseName, PreviousMigration);
        string seed = await File.ReadAllTextAsync(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "Upgrades", "NameKeyUpgradeSeed.sql"));
        await using NpgsqlConnection connection = new(_database.ConnectionString);
        await connection.OpenAsync();
        await using NpgsqlCommand command = new(seed, connection);
        await command.ExecuteNonQueryAsync();
        _services = CreateServices();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Test]
    public async Task Upgrade_LibraryOfTheOldScanner_JoinsEqualNamesAndKeepsEveryUserData()
    {
        Counts before = await CountAsync();

        await _database.MigrateAsync();
        NameKeyUpgradePlan? plan = await RunAsync();

        plan.Should().NotBeNull();
        (plan!.ArtistMerges.Count, plan.AlbumMerges.Count, plan.GenreMerges.Count, plan.SmartPlaylistRules.Count)
            .Should().Be((7, 7, 5, 5));
        Counts after = await CountAsync();
        after.Should().Be(before with
        {
            Artists = before.Artists - 7,
            Albums = before.Albums - 7,
            // Anna liked both K.Flay and K Flay, Ben both albums Back in Black.
            ArtistFavorites = before.ArtistFavorites - 1,
            AlbumFavorites = before.AlbumFavorites - 1,
        });
        (before.Tracks, before.Ratings, before.Plays, before.PlaylistEntries).Should().Be((177, 90, 450, 18));

        await using WaveMpDbContext db = _database.CreateDbContext();
        (await db.Genres.CountAsync()).Should().Be(19);
        (await db.DataUpgrades.SingleAsync(upgrade => upgrade.Name == DataUpgrade.NameKeys)).CompletedAt
            .Should().NotBeNull();
        (await db.ArtistMoves.CountAsync(), await db.AlbumMoves.CountAsync()).Should().Be((0, 0));
    }

    [Test]
    public async Task Upgrade_JoinedEntries_PassTheirDataToTheEntriesThatStay()
    {
        await _database.MigrateAsync();
        await RunAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        Guid kFlay = Id("artist:K.Flay");
        Guid solutions = Id("album:K.Flay|Solutions");
        Guid backInBlack = Id("album:AC/DC|Back in Black");
        Artist artist = await db.Artists.SingleAsync(item => item.Id == kFlay);
        (artist.Name, artist.NormalizedName, artist.MusicBrainzArtistId)
            .Should().Be(("K.Flay", "kflay", Guid.Parse("5b11f4ce-a62d-471e-81fc-a69a8278c7da")));
        (await db.Tracks.CountAsync(track => track.ArtistId == kFlay)).Should().Be(10);
        (await db.Tracks.CountAsync(track => track.AlbumId == solutions)).Should().Be(6);
        Album album = await db.Albums.SingleAsync(item => item.Id == solutions);
        (album.Title, album.NormalizedTitle, album.CoverPriority, album.CoverFallback)
            .Should().Be(("Solutions", "solutions", CoverPriority.TrackFirst, AlbumCoverFallback.None));
        (await db.Albums.SingleAsync(item => item.Id == backInBlack)).CoverPriority
            .Should().Be(CoverPriority.TrackOnly);
        (await db.OnlineCovers.SingleAsync()).AlbumId.Should().Be(backInBlack);
        (await db.ArtistPictures.SingleAsync(picture => picture.Kind == ArtistPictureKind.Uploaded)).ArtistId
            .Should().Be(kFlay);
        (await db.ArtistPictures.SingleAsync(picture => picture.Kind == ArtistPictureKind.Online)).ArtistId
            .Should().Be(Id("artist:Beyoncé"));

        Guid anna = Id("user:Anna");
        (await db.Favorites.Where(favorite => favorite.UserId == anna && favorite.Type == FavoriteType.Artist)
                .Select(favorite => favorite.ArtistId)
                .ToListAsync())
            .Should().BeEquivalentTo([kFlay, Id("artist:Beyoncé")]);
        (await db.Favorites.Where(favorite => favorite.UserId == anna && favorite.Type == FavoriteType.Album)
                .Select(favorite => favorite.AlbumId)
                .ToListAsync())
            .Should().BeEquivalentTo([solutions, Id("album:Beyoncé|Lemonade")]);
        (await db.Favorites.SingleAsync(favorite => favorite.UserId == Id("user:Ben")
                && favorite.Type == FavoriteType.Album))
            .AlbumId.Should().Be(backInBlack);
        (await db.PlaybackStates.SingleAsync(state => state.UserId == anna)).SourceId.Should().Be(solutions);
        (await db.PlaybackStates.SingleAsync(state => state.UserId == Id("user:Ben"))).SourceId
            .Should().Be(Id("artist:AC/DC"));
    }

    [Test]
    public async Task Upgrade_GenresOfTheTracks_AreJoinedByTheirNormalizedNames()
    {
        await _database.MigrateAsync();
        await RunAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        List<string> genres = await db.Genres.OrderBy(genre => genre.NormalizedName)
            .Select(genre => genre.Name + "=" + genre.NormalizedName)
            .ToListAsync();
        genres.Should().Contain(["Hip-Hop=hiphop", "K-Pop=kpop", "R&B=rb", "Folk=folk", "Рок=рок"]);
        Genre hipHop = await db.Genres.SingleAsync(genre => genre.NormalizedName == "hiphop");
        (await db.Tracks.CountAsync(track => track.GenreId == hipHop.Id)).Should().Be(6);
        (await db.Tracks.CountAsync(track => track.GenreId == null)).Should().Be(4);
    }

    [Test]
    public async Task Upgrade_RulesOfSmartPlaylists_GetTheIdsOfTheEntriesTheyName()
    {
        await _database.MigrateAsync();
        await RunAsync();

        await using WaveMpDbContext db = _database.CreateDbContext();
        Dictionary<string, List<SmartPlaylistRule>> rules = await db.SmartPlaylists
            .ToDictionaryAsync(playlist => playlist.Name, playlist => playlist.Rules);
        Guid hipHop = await db.Genres.Where(genre => genre.NormalizedName == "hiphop")
            .Select(genre => genre.Id)
            .SingleAsync();
        Guid kPop = await db.Genres.Where(genre => genre.NormalizedName == "kpop")
            .Select(genre => genre.Id)
            .SingleAsync();
        rules["Only K.Flay"].Single().ReferenceId.Should().Be(Id("artist:K.Flay"));
        rules["Lower Case"].Single().ReferenceId.Should().Be(Id("artist:K.Flay"));
        rules["Not Beyonce"].Single().ReferenceId.Should().Be(Id("artist:Beyoncé"));
        rules["Solutions"].Single().ReferenceId.Should().Be(Id("album:K.Flay|Solutions"));
        rules["Hip Hop"].Select(rule => rule.ReferenceId).Should().Equal(hipHop, kPop);
        rules["Greatest"].Single().ReferenceId.Should().BeNull("two albums are called Greatest Hits");
        rules["Unknown"].Single().ReferenceId.Should().BeNull();
        rules["Contains"].Should().OnlyContain(rule => rule.ReferenceId == null);
        rules["Only K.Flay"].Single().Text.Should().Be("K.Flay");

        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        ISmartPlaylistRepository repository = scope.ServiceProvider.GetRequiredService<ISmartPlaylistRepository>();
        SmartPlaylist onlyKFlay = await db.SmartPlaylists.SingleAsync(playlist => playlist.Name == "Only K.Flay");
        Page<Guid> tracks = await repository.ListTrackIdsAsync(
            onlyKFlay.UserId,
            SmartPlaylistCriteria.Of(onlyKFlay),
            DateTimeOffset.UtcNow,
            0,
            100);
        tracks.Total.Should().Be(10, "the songs tagged K Flay belong to K.Flay now");
    }

    [Test]
    public async Task Upgrade_RunTwice_ChangesNothingTheSecondTime()
    {
        await _database.MigrateAsync();
        await RunAsync();
        Counts first = await CountAsync();

        NameKeyUpgradePlan? second = await RunAsync();
        await _services.UpgradeWaveMpLibraryAsync();

        second.Should().BeNull();
        (await CountAsync()).Should().Be(first);
    }

    private async Task<NameKeyUpgradePlan?> RunAsync()
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NameKeyUpgrader>().RunAsync();
    }

    private async Task<Counts> CountAsync()
    {
        await using WaveMpDbContext db = _database.CreateDbContext();
        return new Counts(
            await db.Artists.CountAsync(),
            await db.Albums.CountAsync(),
            await db.Tracks.CountAsync(),
            await db.Tracks.CountAsync(track => track.ArtistId != null),
            await db.Tracks.CountAsync(track => track.AlbumId != null),
            await db.Favorites.CountAsync(favorite => favorite.Type == FavoriteType.Track),
            await db.Favorites.CountAsync(favorite => favorite.Type == FavoriteType.Album),
            await db.Favorites.CountAsync(favorite => favorite.Type == FavoriteType.Artist),
            await db.TrackRatings.CountAsync(),
            await db.TrackRatings.SumAsync(rating => rating.Rating),
            await db.PlayHistory.CountAsync(),
            await db.PlaylistEntries.CountAsync(),
            await db.SmartPlaylists.CountAsync(),
            await db.ArtistPictures.CountAsync(),
            await db.OnlineCovers.CountAsync(),
            await db.Albums.CountAsync(album => album.CoverPriority != null || album.CoverFallback != null),
            await db.PlaybackStates.CountAsync());
    }

    /// <summary>
    /// The id the seed gives an entry of this name.
    /// </summary>
    private static Guid Id(string name)
    {
        return Guid.ParseExact(Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(name))), "N");
    }

    private static ServiceProvider CreateServices()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(TestDatabaseSettings.ToConfiguration(DatabaseName))
            .Build();
        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddWaveMpData();
        services.AddScoped<NameKeyUpgrader>();
        services.AddScoped<SearchKeyUpgrader>();
        return services.BuildServiceProvider();
    }

    private sealed record Counts(
        int Artists,
        int Albums,
        int Tracks,
        int TracksWithArtist,
        int TracksWithAlbum,
        int TrackFavorites,
        int AlbumFavorites,
        int ArtistFavorites,
        int Ratings,
        int RatingSum,
        int Plays,
        int PlaylistEntries,
        int SmartPlaylists,
        int ArtistPictures,
        int OnlineCovers,
        int AlbumsWithCoverRules,
        int PlaybackStates);
}
