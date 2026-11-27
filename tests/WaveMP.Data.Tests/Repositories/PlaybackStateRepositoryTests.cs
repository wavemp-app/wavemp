using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Databases;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class PlaybackStateRepositoryTests : LibraryDatabaseTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 19, 20, 0, 0, TimeSpan.Zero);

    protected override string DatabaseName => "wavemp_tests_data_playback_states";

    [Test]
    public async Task FindAsync_NothingSaved_ReturnsNull()
    {
        User user = await AddUserAsync();

        PlaybackState? state = await CreateRepository().FindAsync(user.Id);

        state.Should().BeNull();
    }

    [Test]
    public async Task SaveAsync_FirstState_SavesAllValues()
    {
        User user = await AddUserAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var source = Guid.NewGuid();
        PlaybackState state = NewState(user.Id, "phone", _start);
        state.TrackIds = [first, second, first];
        state.AutoplayPositions = [2];
        state.CurrentIndex = 1;
        state.PositionMilliseconds = 61_500;
        state.Shuffle = true;
        state.OriginalOrder = [1, 0, 2];
        state.Repeat = RepeatMode.All;
        state.SourceKind = PlaybackSourceKind.Playlist;
        state.SourceId = source;

        bool saved = await CreateRepository().SaveAsync(state);

        saved.Should().BeTrue();
        PlaybackState? loaded = await CreateRepository().FindAsync(user.Id);
        loaded.Should().NotBeNull();
        loaded!.TrackIds.Should().Equal(first, second, first);
        loaded.AutoplayPositions.Should().Equal(2);
        loaded.CurrentIndex.Should().Be(1);
        loaded.PositionMilliseconds.Should().Be(61_500);
        loaded.Shuffle.Should().BeTrue();
        loaded.OriginalOrder.Should().Equal(1, 0, 2);
        loaded.Repeat.Should().Be(RepeatMode.All);
        loaded.SourceKind.Should().Be(PlaybackSourceKind.Playlist);
        loaded.SourceId.Should().Be(source);
        loaded.DeviceId.Should().Be("phone");
        loaded.DeviceName.Should().Be("Device phone");
        loaded.ChangedAt.Should().Be(_start);
    }

    [Test]
    public async Task SaveAsync_NewerState_ReplacesTheSavedOne()
    {
        User user = await AddUserAsync();
        IPlaybackStateRepository repository = CreateRepository();
        await repository.SaveAsync(NewState(user.Id, "phone", _start));
        PlaybackState newer = NewState(user.Id, "laptop", _start.AddSeconds(10));
        newer.SourceKind = null;
        newer.SourceId = null;

        bool saved = await CreateRepository().SaveAsync(newer);

        saved.Should().BeTrue();
        PlaybackState? loaded = await CreateRepository().FindAsync(user.Id);
        loaded!.DeviceId.Should().Be("laptop");
        loaded.ChangedAt.Should().Be(_start.AddSeconds(10));
        loaded.SourceKind.Should().BeNull();
        loaded.SourceId.Should().BeNull();
    }

    [Test]
    public async Task SaveAsync_StateChangedAtTheSameTime_ReplacesTheSavedOne()
    {
        User user = await AddUserAsync();
        await CreateRepository().SaveAsync(NewState(user.Id, "phone", _start));

        bool saved = await CreateRepository().SaveAsync(NewState(user.Id, "laptop", _start));

        saved.Should().BeTrue();
        (await CreateRepository().FindAsync(user.Id))!.DeviceId.Should().Be("laptop");
    }

    [Test]
    public async Task SaveAsync_OlderState_KeepsTheSavedOne()
    {
        User user = await AddUserAsync();
        await CreateRepository().SaveAsync(NewState(user.Id, "phone", _start));

        bool saved = await CreateRepository().SaveAsync(NewState(user.Id, "laptop", _start.AddSeconds(-1)));

        saved.Should().BeFalse();
        PlaybackState? loaded = await CreateRepository().FindAsync(user.Id);
        loaded!.DeviceId.Should().Be("phone");
        loaded.ChangedAt.Should().Be(_start);
    }

    [Test]
    public async Task SaveAsync_Always_KeepsTheStatesOfOtherUsers()
    {
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        await CreateRepository().SaveAsync(NewState(other.Id, "tablet", _start));

        await CreateRepository().SaveAsync(NewState(user.Id, "phone", _start.AddMinutes(1)));

        (await CreateRepository().FindAsync(other.Id))!.DeviceId.Should().Be("tablet");
        (await CreateRepository().FindAsync(user.Id))!.DeviceId.Should().Be("phone");
    }

    [Test]
    public async Task SaveAsync_UnknownUser_ThrowsUserNotFound()
    {
        var userId = Guid.NewGuid();

        Func<Task> save = () => CreateRepository().SaveAsync(NewState(userId, "phone", _start));

        (await save.Should().ThrowAsync<UserNotFoundException>()).Which.UserId.Should().Be(userId);
    }

    [Test]
    public async Task SaveAsync_UserDeletedWhileSaving_ThrowsUserNotFoundAndSavesNothing()
    {
        User user = await AddUserAsync();
        await using PendingDeletion deletion =
            await PendingDeletion.OfUserAsync(CreateDbContext().Database.GetConnectionString()!, user.Id);

        Task<bool> save = CreateRepository().SaveAsync(NewState(user.Id, "phone", _start));
        bool waited = await deletion.CommitWhileWaitingAsync(save, TimeSpan.FromSeconds(10));

        waited.Should().BeTrue();
        await FluentActions.Awaiting(() => save).Should().ThrowAsync<UserNotFoundException>();
        await using WaveMpDbContext dbContext = CreateDbContext();
        (await dbContext.PlaybackStates.AnyAsync(state => state.UserId == user.Id)).Should().BeFalse();
    }

    [Test]
    public async Task DeleteAsync_SavedState_RemovesItAndKeepsTheStatesOfOtherUsers()
    {
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        await CreateRepository().SaveAsync(NewState(user.Id, "phone", _start));
        await CreateRepository().SaveAsync(NewState(other.Id, "tablet", _start));

        bool deleted = await CreateRepository().DeleteAsync(user.Id);

        deleted.Should().BeTrue();
        (await CreateRepository().FindAsync(user.Id)).Should().BeNull();
        (await CreateRepository().FindAsync(other.Id))!.DeviceId.Should().Be("tablet");
    }

    [Test]
    public async Task DeleteAsync_NothingSaved_ReturnsFalse()
    {
        User user = await AddUserAsync();

        bool deleted = await CreateRepository().DeleteAsync(user.Id);

        deleted.Should().BeFalse();
    }

    [Test]
    public async Task SaveAsync_AfterDelete_SavesAnOlderStateAgain()
    {
        User user = await AddUserAsync();
        await CreateRepository().SaveAsync(NewState(user.Id, "phone", _start));
        await CreateRepository().DeleteAsync(user.Id);

        bool saved = await CreateRepository().SaveAsync(NewState(user.Id, "laptop", _start.AddSeconds(-1)));

        saved.Should().BeTrue();
        (await CreateRepository().FindAsync(user.Id))!.DeviceId.Should().Be("laptop");
    }

    [Test]
    public async Task DeletingTheUser_Always_DeletesItsState()
    {
        User user = await AddUserAsync();
        await CreateRepository().SaveAsync(NewState(user.Id, "phone", _start));

        await using (WaveMpDbContext dbContext = CreateDbContext())
        {
            await dbContext.Users.Where(candidate => candidate.Id == user.Id).ExecuteDeleteAsync();
        }

        (await CreateRepository().FindAsync(user.Id)).Should().BeNull();
    }

    private IPlaybackStateRepository CreateRepository()
    {
        return new PlaybackStateRepository(CreateDbContext());
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

    private static PlaybackState NewState(Guid userId, string deviceId, DateTimeOffset changedAt)
    {
        return new PlaybackState
        {
            UserId = userId,
            TrackIds = [Guid.NewGuid()],
            CurrentIndex = 0,
            PositionMilliseconds = 1_000,
            Shuffle = false,
            Repeat = RepeatMode.Off,
            SourceKind = PlaybackSourceKind.Album,
            SourceId = Guid.NewGuid(),
            DeviceId = deviceId,
            DeviceName = "Device " + deviceId,
            ChangedAt = changedAt,
        };
    }
}
