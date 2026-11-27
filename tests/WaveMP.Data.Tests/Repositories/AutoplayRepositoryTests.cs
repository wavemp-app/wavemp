using AwesomeAssertions;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Seeds;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class AutoplayRepositoryTests : LibraryDatabaseTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 19, 20, 0, 0, TimeSpan.Zero);

    protected override string DatabaseName => "wavemp_tests_autoplay";

    [Test]
    public async Task ListSeedsAsync_TracksOfTheList_ReturnsTheirArtistGenreAndYear()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();

        IReadOnlyList<AutoplaySeedRow> seeds = await CreateRepository().ListSeedsAsync(
            [library.Opening.Id, library.Ballad.Id, library.Opening.Id, Guid.NewGuid()]);

        seeds.Should().BeEquivalentTo(
        [
            new AutoplaySeedRow(library.Opening.Id, [library.Alpha.Id], "Rock", 2001),
            new AutoplaySeedRow(library.Ballad.Id, [library.Beta.Id], "Pop", 2010),
        ]);
    }

    [Test]
    public async Task ListCandidatesAsync_ByArtists_ReturnsTheirTracksWithFavoriteAndRating()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        LibrarySeed seed = new();
        User user = seed.User();
        seed.Favorite(user, library.HitOne);
        seed.Rating(user, library.HitOne, 5);
        seed.Rating(user, library.Closing, 3);
        await SaveAsync(seed);

        IReadOnlyList<AutoplayCandidateRow> candidates = await CreateRepository().ListCandidatesAsync(
            new AutoplayCandidateQuery(user.Id, 100, true, [library.Opening.Id], ArtistIds: [library.Alpha.Id]));

        candidates.Should().BeEquivalentTo(
        [
            new AutoplayCandidateRow(library.Closing.Id, [library.Alpha.Id], "Rock", 2001, true, false, 3),
            new AutoplayCandidateRow(library.HitOne.Id, [library.Alpha.Id], "Pop", 2005, true, true, 5),
        ]);
    }

    [Test]
    public async Task ListCandidatesAsync_TrackRatedWithOneStar_IsLeftOutForThatUserOnly()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        LibrarySeed seed = new();
        User user = seed.User();
        User other = seed.User();
        seed.Rating(user, library.Closing, 1);
        await SaveAsync(seed);
        IAutoplayRepository repository = CreateRepository();

        IReadOnlyList<AutoplayCandidateRow> own = await repository.ListCandidatesAsync(
            new AutoplayCandidateQuery(user.Id, 100, true, [], ArtistIds: [library.Alpha.Id]));
        IReadOnlyList<AutoplayCandidateRow> foreign = await repository.ListCandidatesAsync(
            new AutoplayCandidateQuery(other.Id, 100, true, [], ArtistIds: [library.Alpha.Id]));

        own.Select(row => row.TrackId).Should().BeEquivalentTo([library.Opening.Id, library.HitOne.Id]);
        foreign.Select(row => row.TrackId).Should()
            .BeEquivalentTo([library.Opening.Id, library.Closing.Id, library.HitOne.Id]);
    }

    [Test]
    public async Task ListCandidatesAsync_WithoutVideoOnly_LeavesOutTracksWithAMusicVideoOnly()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        IAutoplayRepository repository = CreateRepository();

        IReadOnlyList<AutoplayCandidateRow> with = await repository.ListCandidatesAsync(
            new AutoplayCandidateQuery(user.Id, 100, true, [], ArtistIds: [library.Beta.Id]));
        IReadOnlyList<AutoplayCandidateRow> without = await repository.ListCandidatesAsync(
            new AutoplayCandidateQuery(user.Id, 100, false, [], ArtistIds: [library.Beta.Id]));

        with.Select(row => row.TrackId).Should()
            .BeEquivalentTo([library.Ballad.Id, library.HitTwo.Id, library.Clip.Id]);
        with.Single(row => row.TrackId == library.Clip.Id).HasAudio.Should().BeFalse();
        without.Select(row => row.TrackId).Should().BeEquivalentTo([library.Ballad.Id, library.HitTwo.Id]);
    }

    [Test]
    public async Task ListCandidatesAsync_ByGenresAndDecades_MatchesTheGenreWithoutRegardToCase()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<AutoplayCandidateRow> candidates = await CreateRepository().ListCandidatesAsync(
            new AutoplayCandidateQuery(user.Id, 100, true, [library.Duet.Id], Genres: ["ROCK"], Decades: [2000]));

        candidates.Select(row => row.TrackId).Should().BeEquivalentTo([library.Opening.Id, library.Closing.Id]);
    }

    [Test]
    public async Task ListCandidatesAsync_ByDecades_LeavesOutTracksOfOtherDecadesAndWithoutYear()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<AutoplayCandidateRow> candidates = await CreateRepository().ListCandidatesAsync(
            new AutoplayCandidateQuery(user.Id, 100, true, [], Genres: ["pop"], Decades: [2010]));

        candidates.Select(row => row.TrackId).Should().BeEquivalentTo([library.Ballad.Id]);
    }

    [Test]
    public async Task ListCandidatesAsync_MoreTracksThanTheLimit_ReturnsTheLimit()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        IReadOnlyList<AutoplayCandidateRow> candidates = await CreateRepository().ListCandidatesAsync(
            new AutoplayCandidateQuery(user.Id, 2, true, [], ArtistIds: [library.Alpha.Id, library.Beta.Id]));

        candidates.Should().HaveCount(2);
        candidates.SelectMany(row => row.ArtistIds).Should().OnlyContain(
            id => id == library.Alpha.Id || id == library.Beta.Id);
    }

    [Test]
    public async Task ListRecentPlaysAsync_Always_ReturnsTheLastPlayOfEachTrackSinceTheTimeOfTheUser()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        await AddPlaysAsync(
            (user, library.Opening, _now.AddHours(-30)),
            (user, library.Opening, _now.AddHours(-2)),
            (user, library.Opening, _now.AddHours(-1)),
            (user, library.Ballad, _now.AddHours(-25)),
            (other, library.Duet, _now.AddHours(-1)));

        IReadOnlyDictionary<Guid, DateTimeOffset> plays = await CreateRepository()
            .ListRecentPlaysAsync(user.Id, _now.AddHours(-24));

        plays.Should().BeEquivalentTo(
            new Dictionary<Guid, DateTimeOffset> { [library.Opening.Id] = _now.AddHours(-1) });
    }

    private IAutoplayRepository CreateRepository()
    {
        return new AutoplayRepository(CreateDbContext());
    }

    private async Task<User> AddUserAsync()
    {
        LibrarySeed seed = new();
        User user = seed.User();
        await SaveAsync(seed);
        return user;
    }

    private async Task AddPlaysAsync(params (User User, Track Track, DateTimeOffset PlayedAt)[] plays)
    {
        WaveMpDbContext dbContext = CreateDbContext();
        dbContext.PlayHistory.AddRange(plays.Select(play => new PlayHistoryEntry
        {
            Id = Guid.CreateVersion7(),
            UserId = play.User.Id,
            TrackId = play.Track.Id,
            PlayedAt = play.PlayedAt,
        }));
        await dbContext.SaveChangesAsync();
    }
}
