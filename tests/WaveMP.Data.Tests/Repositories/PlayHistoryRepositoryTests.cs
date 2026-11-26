using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Queries;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Databases;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class PlayHistoryRepositoryTests : LibraryDatabaseTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 1, 20, 0, 0, TimeSpan.Zero);

    protected override string DatabaseName => "wavemp_tests_play_history";

    [Test]
    public async Task ListAsync_Always_ReturnsTheNewestPlayFirst()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        PlayHistoryEntry older = await AddEntryAsync(user, library.Opening, _start);
        PlayHistoryEntry newer = await AddEntryAsync(user, library.Ballad, _start.AddMinutes(5));
        PlayHistoryEntry newest = await AddEntryAsync(user, library.Opening, _start.AddMinutes(10));

        Page<PlayHistoryEntry> page = await CreateRepository().ListAsync(new PlayHistoryListQuery(user.Id, 0, 10));

        page.Items.Select(entry => entry.Id).Should().Equal(newest.Id, newer.Id, older.Id);
        page.Total.Should().Be(3);
    }

    [Test]
    public async Task ListAsync_WithOffsetAndLimit_ReturnsOnePage()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        List<PlayHistoryEntry> entries = [];
        for (int index = 0; index < 5; index++)
        {
            entries.Add(await AddEntryAsync(user, library.Opening, _start.AddMinutes(index)));
        }

        Page<PlayHistoryEntry> page = await CreateRepository().ListAsync(new PlayHistoryListQuery(user.Id, 1, 2));

        page.Items.Select(entry => entry.Id).Should().Equal(entries[3].Id, entries[2].Id);
        page.Total.Should().Be(5);
        page.Offset.Should().Be(1);
        page.Limit.Should().Be(2);
    }

    [Test]
    public async Task ListAsync_Always_OnlyReturnsTheEntriesOfTheUser()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        PlayHistoryEntry own = await AddEntryAsync(user, library.Opening, _start);
        await AddEntryAsync(other, library.Opening, _start);

        Page<PlayHistoryEntry> page = await CreateRepository().ListAsync(new PlayHistoryListQuery(user.Id, 0, 10));

        page.Items.Should().ContainSingle().Which.Id.Should().Be(own.Id);
    }

    [Test]
    public async Task FindAsync_EntryOfAnotherUser_ReturnsNull()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        PlayHistoryEntry entry = await AddEntryAsync(user, library.Opening, _start);

        IPlayHistoryRepository repository = CreateRepository();
        PlayHistoryEntry? own = await repository.FindAsync(user.Id, entry.Id);
        PlayHistoryEntry? foreign = await repository.FindAsync(other.Id, entry.Id);

        own.Should().NotBeNull();
        own!.TrackId.Should().Be(library.Opening.Id);
        own.PlayedAt.Should().Be(_start);
        foreign.Should().BeNull();
    }

    [Test]
    public async Task AddAsync_UnknownTrack_ReturnsFalseAndAddsNothing()
    {
        await SeedStandardLibraryAsync();
        User user = await AddUserAsync();

        bool added = await CreateRepository().AddAsync(NewEntry(user.Id, Guid.NewGuid()));

        added.Should().BeFalse();
        (await CreateRepository().ListAsync(new PlayHistoryListQuery(user.Id, 0, 10))).Items.Should().BeEmpty();
    }

    [Test]
    public async Task AddAsync_UnknownUser_ThrowsUserNotFound()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        var userId = Guid.NewGuid();

        Func<Task> add = () => CreateRepository().AddAsync(NewEntry(userId, library.Opening.Id));

        (await add.Should().ThrowAsync<UserNotFoundException>()).Which.UserId.Should().Be(userId);
    }

    [Test]
    public async Task AddAsync_UserDeletedWhileAdding_ThrowsUserNotFoundAndAddsNothing()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        await using PendingDeletion deletion =
            await PendingDeletion.OfUserAsync(CreateDbContext().Database.GetConnectionString()!, user.Id);

        Task<bool> add = CreateRepository().AddAsync(NewEntry(user.Id, library.Opening.Id));
        bool waited = await deletion.CommitWhileWaitingAsync(add, TimeSpan.FromSeconds(10));

        waited.Should().BeTrue();
        await FluentActions.Awaiting(() => add).Should().ThrowAsync<UserNotFoundException>();
        await using WaveMpDbContext dbContext = CreateDbContext();
        (await dbContext.PlayHistory.AnyAsync(entry => entry.UserId == user.Id)).Should().BeFalse();
    }

    [Test]
    public async Task DeletingTheUser_Always_DeletesItsHistory()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        PlayHistoryEntry entry = await AddEntryAsync(user, library.Opening, _start);

        await using (WaveMpDbContext dbContext = CreateDbContext())
        {
            await dbContext.Users.Where(candidate => candidate.Id == user.Id).ExecuteDeleteAsync();
        }

        (await CreateRepository().FindAsync(user.Id, entry.Id)).Should().BeNull();
    }

    [Test]
    public async Task DeletingTheTrack_Always_DeletesItsHistory()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        PlayHistoryEntry removed = await AddEntryAsync(user, library.Opening, _start);
        PlayHistoryEntry kept = await AddEntryAsync(user, library.Ballad, _start);

        await using (WaveMpDbContext dbContext = CreateDbContext())
        {
            await dbContext.Tracks.Where(track => track.Id == library.Opening.Id).ExecuteDeleteAsync();
        }

        Page<PlayHistoryEntry> page = await CreateRepository().ListAsync(new PlayHistoryListQuery(user.Id, 0, 10));
        page.Items.Select(entry => entry.Id).Should().Equal(kept.Id);
        page.Items.Should().NotContain(entry => entry.Id == removed.Id);
    }

    [Test]
    public async Task ListRecentTracksAsync_Always_ListsEachTrackOnceByItsLastPlay()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        await AddEntryAsync(user, library.Opening, _start);
        await AddEntryAsync(user, library.Ballad, _start.AddMinutes(5));
        await AddEntryAsync(user, library.Opening, _start.AddMinutes(10));
        await AddEntryAsync(user, library.Duet, _start.AddMinutes(1));
        await AddEntryAsync(other, library.Closing, _start.AddMinutes(20));

        Page<RecentTrackRow> page = await CreateRepository().ListRecentTracksAsync(
            new RecentTrackListQuery(user.Id, 0, 10, 100));

        page.Items.Should().Equal(
            new RecentTrackRow(library.Opening.Id, _start.AddMinutes(10)),
            new RecentTrackRow(library.Ballad.Id, _start.AddMinutes(5)),
            new RecentTrackRow(library.Duet.Id, _start.AddMinutes(1)));
        page.Total.Should().Be(3);
    }

    [Test]
    public async Task ListRecentTracksAsync_MoreTracksThanTheMaximum_CutsTheList()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        await AddEntryAsync(user, library.Opening, _start);
        await AddEntryAsync(user, library.Ballad, _start.AddMinutes(1));
        await AddEntryAsync(user, library.Duet, _start.AddMinutes(2));

        IPlayHistoryRepository repository = CreateRepository();
        Page<RecentTrackRow> first = await repository.ListRecentTracksAsync(new RecentTrackListQuery(user.Id, 0, 1, 2));
        Page<RecentTrackRow> second = await repository.ListRecentTracksAsync(
            new RecentTrackListQuery(user.Id, 1, 5, 2));
        Page<RecentTrackRow> beyond = await repository.ListRecentTracksAsync(
            new RecentTrackListQuery(user.Id, 2, 5, 2));

        first.Items.Select(row => row.TrackId).Should().Equal(library.Duet.Id);
        second.Items.Select(row => row.TrackId).Should().Equal(library.Ballad.Id);
        beyond.Items.Should().BeEmpty();
        first.Total.Should().Be(2);
        beyond.Total.Should().Be(2);
    }

    [Test]
    public async Task ListPlaysAsync_WithBounds_ListsThePlaysOfTheUserInThePeriodWithTheirTracks()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        await AddEntryAsync(user, library.Opening, _start.AddMinutes(-1));
        await AddEntryAsync(user, library.Ballad, _start);
        await AddEntryAsync(user, library.Opening, _start.AddMinutes(30));
        await AddEntryAsync(user, library.Closing, _start.AddHours(1));
        await AddEntryAsync(other, library.Duet, _start.AddMinutes(10));

        IReadOnlyList<PlayStatisticsRow> plays =
            await CreateRepository().ListPlaysAsync(user.Id, _start, _start.AddHours(1));

        plays.Should().Equal(
            new PlayStatisticsRow(
                _start,
                library.Ballad.Id,
                "Ballad",
                library.Beta.Id,
                "Beta Singer",
                library.SecondAlbum.Id,
                "Second Album",
                library.Beta.Id,
                "Beta Singer",
                library.Ballad.GenreId,
                "Pop",
                TimeSpan.FromMinutes(5)),
            new PlayStatisticsRow(
                _start.AddMinutes(30),
                library.Opening.Id,
                "Opening",
                library.Alpha.Id,
                "Alpha Band",
                library.FirstAlbum.Id,
                "First Album",
                library.Alpha.Id,
                "Alpha Band",
                library.Opening.GenreId,
                "Rock",
                TimeSpan.FromMinutes(3)));
    }

    [Test]
    public async Task ListPlaysAsync_WithoutBounds_ListsAllPlaysOfTheUserOldestFirst()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        await AddEntryAsync(user, library.LooseSong, _start.AddDays(1));
        await AddEntryAsync(user, library.Opening, _start);

        IReadOnlyList<PlayStatisticsRow> plays = await CreateRepository().ListPlaysAsync(user.Id, null, null);

        plays.Select(play => play.TrackId).Should().Equal(library.Opening.Id, library.LooseSong.Id);
        plays[1].AlbumId.Should().BeNull();
        plays[1].AlbumTitle.Should().BeNull();
        plays[1].Genre.Should().BeNull();
    }

    [Test]
    public async Task FindFirstPlaysOfArtistsAsync_Always_ReturnsTheFirstPlayOfEachArtistOfTheUser()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        await AddEntryAsync(other, library.Opening, _start.AddDays(-5));
        await AddEntryAsync(user, library.Closing, _start.AddDays(2));
        await AddEntryAsync(user, library.Opening, _start);
        await AddEntryAsync(user, library.Ballad, _start.AddDays(1));

        IReadOnlyDictionary<Guid, DateTimeOffset> first = await CreateRepository().FindFirstPlaysOfArtistsAsync(
            user.Id,
            [library.Alpha.Id, library.Guest.Id]);

        first.Should().BeEquivalentTo(new Dictionary<Guid, DateTimeOffset> { [library.Alpha.Id] = _start });
    }

    [Test]
    public async Task FindFirstPlayAsync_Always_ReturnsTheFirstPlayOfTheUserOrNull()
    {
        StandardLibrary library = await SeedStandardLibraryAsync();
        User user = await AddUserAsync();
        User silent = await AddUserAsync();
        await AddEntryAsync(user, library.Closing, _start.AddDays(2));
        await AddEntryAsync(user, library.Opening, _start);

        IPlayHistoryRepository repository = CreateRepository();

        (await repository.FindFirstPlayAsync(user.Id)).Should().Be(_start);
        (await repository.FindFirstPlayAsync(silent.Id)).Should().BeNull();
    }

    private IPlayHistoryRepository CreateRepository()
    {
        return new PlayHistoryRepository(CreateDbContext());
    }

    private async Task<User> AddUserAsync()
    {
        string name = "Listener " + Guid.NewGuid().ToString("N")[..8];
        User user = new()
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            NormalizedName = name.ToUpperInvariant(),
            Initials = "L",
            Color = "#336699",
            Role = UserRole.User,
            IsBuiltIn = false,
            CreatedAt = _start,
        };
        await using WaveMpDbContext dbContext = CreateDbContext();
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user;
    }

    private async Task<PlayHistoryEntry> AddEntryAsync(User user, Track track, DateTimeOffset playedAt)
    {
        PlayHistoryEntry entry = NewEntry(user.Id, track.Id, playedAt);
        (await CreateRepository().AddAsync(entry)).Should().BeTrue();
        return entry;
    }

    private static PlayHistoryEntry NewEntry(Guid userId, Guid trackId, DateTimeOffset? playedAt = null)
    {
        return new PlayHistoryEntry
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            TrackId = trackId,
            PlayedAt = playedAt ?? _start,
        };
    }
}
