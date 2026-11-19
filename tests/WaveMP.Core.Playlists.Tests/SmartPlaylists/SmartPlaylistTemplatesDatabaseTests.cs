using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WaveMP.Core.Caching.Extensions;
using WaveMP.Core.Playlists.Extensions;
using WaveMP.Core.Playlists.SmartPlaylists;
using WaveMP.Data;
using WaveMP.Data.Entities;
using WaveMP.Data.Extensions;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Testing.Databases;
using WaveMP.Testing.Files;
using WaveMP.Testing.Seeds;

namespace WaveMP.Core.Playlists.Tests.SmartPlaylists;

/// <summary>
/// Every built-in template as a smart playlist of a profile, against a library with ratings, favorites and plays of
/// two profiles in a real PostgreSQL database.
/// </summary>
[TestFixture]
[Category("Integration")]
public class SmartPlaylistTemplatesDatabaseTests
{
    private const string DatabaseName = "wavemp_tests_smart_playlist_templates";

    private static readonly DateTimeOffset _now = new(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);

    private TestDatabase _database = null!;
    private TemporaryDirectory _data = null!;
    private ServiceProvider _services = null!;
    private Library _library = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _database = await TestDatabase.CreateMigratedAsync(DatabaseName);
        _data = new TemporaryDirectory();
        _services = CreateServices(_data.Path);
        _library = await SeedAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _services.DisposeAsync();
        _data.Dispose();
        await _database.DisposeAsync();
    }

    [Test]
    public async Task FourStarsAndMore_RatedTracksOfTheUser_BestFirst()
    {
        IReadOnlyList<string> titles = await TitlesOfAsync(SmartPlaylistTemplateId.FourStarsAndMore);

        titles.Should().Equal("Five Stars", "Four Stars");
    }

    [Test]
    public async Task NeverPlayed_TracksWithoutPlaysOfTheUser_NewestFirst()
    {
        IReadOnlyList<string> titles = await TitlesOfAsync(SmartPlaylistTemplateId.NeverPlayed);

        // Played By Other was only played by the other profile.
        titles.Should().Equal("Brand New", "Four Stars", "Played By Other", "Two Stars", "Old Favorite");
    }

    [Test]
    public async Task RecentlyAdded_TracksOfTheLastThirtyDays_NewestFirst()
    {
        IReadOnlyList<string> titles = await TitlesOfAsync(SmartPlaylistTemplateId.RecentlyAdded);

        titles.Should().Equal("Brand New", "Four Stars");
    }

    [Test]
    public async Task MostPlayed_PlayedTracksOfTheUser_MostPlaysFirst()
    {
        IReadOnlyList<string> titles = await TitlesOfAsync(SmartPlaylistTemplateId.MostPlayed);

        titles.Should().Equal("Hit", "Five Stars");
    }

    [Test]
    public async Task Favorites_FavoriteTracksOfTheUser_ByArtist()
    {
        IReadOnlyList<string> titles = await TitlesOfAsync(SmartPlaylistTemplateId.Favorites);

        titles.Should().Equal("Hit", "Old Favorite");
    }

    [Test]
    public async Task MostPlayed_MoreTracksThanItsLimit_KeepsTheFiftyMostPlayed()
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        ISmartPlaylistService service = scope.ServiceProvider.GetRequiredService<ISmartPlaylistService>();
        SmartPlaylistTemplate template = SmartPlaylistTemplates.Get(SmartPlaylistTemplateId.MostPlayed);

        template.Criteria.Limit.Should().Be(SmartPlaylistTemplates.MostPlayedLimit);
        SmartPlaylistItem item = await CreateAsync(service, _library.User, template with
        {
            Criteria = template.Criteria with { Limit = 1 },
        });

        item.Summary.TrackCount.Should().Be(1);
        Page<TrackRow>? tracks = await service.ListTracksAsync(_library.User.Id, item.Playlist.Id, 0, 10);
        tracks!.Items.Select(track => track.Title).Should().Equal("Hit");
    }

    private async Task<IReadOnlyList<string>> TitlesOfAsync(SmartPlaylistTemplateId id)
    {
        await using AsyncServiceScope scope = _services.CreateAsyncScope();
        ISmartPlaylistService service = scope.ServiceProvider.GetRequiredService<ISmartPlaylistService>();
        SmartPlaylistItem item = await CreateAsync(service, _library.User, SmartPlaylistTemplates.Get(id));

        Page<TrackRow>? tracks = await service.ListTracksAsync(_library.User.Id, item.Playlist.Id, 0, 100);

        tracks.Should().NotBeNull();
        tracks!.Total.Should().Be(item.Summary.TrackCount);
        return [.. tracks.Items.Select(track => track.Title)];
    }

    private static async Task<SmartPlaylistItem> CreateAsync(
        ISmartPlaylistService service,
        User user,
        SmartPlaylistTemplate template)
    {
        SmartPlaylistResult result = await service.CreateAsync(user.Id, new SmartPlaylistDefinition(
            template.Id.ToString(),
            template.Criteria.Match,
            template.Criteria.Rules,
            template.Criteria.Sort,
            template.Criteria.Direction,
            template.Criteria.Limit));
        result.IsOk.Should().BeTrue();
        return result.Item!;
    }

    /// <summary>
    /// Seven tracks of three artists, with ratings, favorites and plays of the user and of another profile that must
    /// not count.
    /// </summary>
    private async Task<Library> SeedAsync()
    {
        await using WaveMpDbContext dbContext = _database.CreateDbContext();
        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE covers, media_files, tracks, albums, artists RESTART IDENTITY CASCADE");
        LibrarySeed seed = new();
        Artist alpha = seed.Artist("Alpha");
        Artist beta = seed.Artist("Beta");
        Artist gamma = seed.Artist("Gamma");
        User user = seed.User();
        User other = seed.User();

        Track fiveStars = seed.Track("Five Stars", beta, firstSeenAt: _now.AddDays(-100));
        Track fourStars = seed.Track("Four Stars", gamma, firstSeenAt: _now.AddDays(-20));
        Track twoStars = seed.Track("Two Stars", alpha, firstSeenAt: _now.AddDays(-200));
        Track hit = seed.Track("Hit", alpha, firstSeenAt: _now.AddDays(-300));
        Track oldFavorite = seed.Track("Old Favorite", gamma, firstSeenAt: _now.AddDays(-400));
        Track brandNew = seed.Track("Brand New", beta, firstSeenAt: _now.AddDays(-1));
        Track playedByOther = seed.Track("Played By Other", beta, firstSeenAt: _now.AddDays(-150));

        seed.Rating(user, fiveStars, 5);
        seed.Rating(user, fourStars, 4);
        seed.Rating(user, twoStars, 2);
        seed.Rating(other, playedByOther, 5);

        seed.Favorite(user, oldFavorite);
        seed.Favorite(user, hit);
        seed.Favorite(other, brandNew);

        seed.Play(user, hit, _now.AddDays(-3));
        seed.Play(user, hit, _now.AddDays(-2));
        seed.Play(user, hit, _now.AddDays(-1));
        seed.Play(user, fiveStars, _now.AddDays(-5));
        seed.Play(other, playedByOther, _now.AddDays(-1));
        seed.Play(other, playedByOther, _now.AddDays(-1));
        seed.Play(other, playedByOther, _now.AddDays(-1));
        seed.Play(other, playedByOther, _now.AddDays(-1));

        await seed.SaveAsync(dbContext);
        return new Library(user);
    }

    private static ServiceProvider CreateServices(string dataPath)
    {
        Dictionary<string, string?> settings = TestDatabaseSettings.ToConfiguration(DatabaseName);
        settings["DATA_PATH"] = dataPath;
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        TimeProvider timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(_now);

        ServiceCollection services = new();
        services.AddSingleton(configuration);
        services.AddSingleton(timeProvider);
        services.AddLogging();
        services.AddWaveMpData();
        services.AddWaveMpCaching();
        services.AddWaveMpPlaylists();
        return services.BuildServiceProvider();
    }

    private sealed record Library(User User);
}
