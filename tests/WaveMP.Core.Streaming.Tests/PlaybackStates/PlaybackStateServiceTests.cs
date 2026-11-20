using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Streaming.Tests.PlaybackStates;

[TestFixture]
public class PlaybackStateServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 19, 20, 0, 0, TimeSpan.Zero);

    private readonly Guid _userId = Guid.NewGuid();

    private IPlaybackStateRepository _states = null!;
    private ITrackRepository _tracks = null!;
    private PlaybackStateService _service = null!;
    private PlaybackState? _saved;

    [SetUp]
    public void SetUp()
    {
        _saved = null;
        _states = Substitute.For<IPlaybackStateRepository>();
        _states.SaveAsync(Arg.Do<PlaybackState>(state => _saved = state), Arg.Any<CancellationToken>()).Returns(true);
        _tracks = Substitute.For<ITrackRepository>();
        TimeProvider timeProvider = Substitute.For<TimeProvider>();
        timeProvider.GetUtcNow().Returns(_now);
        _service = new PlaybackStateService(_states, _tracks, timeProvider);
    }

    [Test]
    public async Task SaveAsync_ValidState_SavesItForTheUser()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var album = Guid.NewGuid();
        SavePlaybackStateCommand command = Command(
            [new(first, false), new(second, false), new(first, true)],
            currentIndex: 1,
            changedAt: _now.AddSeconds(-5)) with
        {
            PositionMilliseconds = 42_000,
            Repeat = RepeatMode.One,
            Source = new PlaybackSource(PlaybackSourceKind.Album, album),
        };

        PlaybackStateSaveStatus status = await _service.SaveAsync(_userId, command);

        status.Should().Be(PlaybackStateSaveStatus.Saved);
        _saved.Should().NotBeNull();
        _saved!.UserId.Should().Be(_userId);
        _saved.TrackIds.Should().Equal(first, second, first);
        _saved.AutoplayPositions.Should().Equal(2);
        _saved.CurrentIndex.Should().Be(1);
        _saved.PositionMilliseconds.Should().Be(42_000);
        _saved.Repeat.Should().Be(RepeatMode.One);
        _saved.SourceKind.Should().Be(PlaybackSourceKind.Album);
        _saved.SourceId.Should().Be(album);
        _saved.DeviceId.Should().Be("phone");
        _saved.DeviceName.Should().Be("Safari · iPhone");
        _saved.ChangedAt.Should().Be(_now.AddSeconds(-5));
    }

    [Test]
    public async Task SaveAsync_WithoutChangedAt_UsesNow()
    {
        await _service.SaveAsync(_userId, Command([new(Guid.NewGuid(), false)], 0, changedAt: null));

        _saved!.ChangedAt.Should().Be(_now);
    }

    [Test]
    public async Task SaveAsync_ChangedAtInTheFuture_UsesNow()
    {
        await _service.SaveAsync(_userId, Command([new(Guid.NewGuid(), false)], 0, changedAt: _now.AddMinutes(3)));

        _saved!.ChangedAt.Should().Be(_now);
    }

    [Test]
    public async Task SaveAsync_ChangedAtWithTicks_KeepsMicroseconds()
    {
        await _service.SaveAsync(
            _userId,
            Command([new(Guid.NewGuid(), false)], 0, changedAt: _now.AddSeconds(-1).AddTicks(1_234_567)));

        _saved!.ChangedAt.Should().Be(_now.AddSeconds(-1).AddTicks(1_234_560));
    }

    [Test]
    public async Task SaveAsync_Shuffled_KeepsTheOrderBeforeShuffling()
    {
        SavePlaybackStateCommand command = Command(
            [new(Guid.NewGuid(), false), new(Guid.NewGuid(), false)],
            0,
            _now) with
        { Shuffle = true, OriginalOrder = [1, 0] };

        await _service.SaveAsync(_userId, command);

        _saved!.Shuffle.Should().BeTrue();
        _saved.OriginalOrder.Should().Equal(1, 0);
    }

    [Test]
    public async Task SaveAsync_NotShuffled_DropsTheOrderBeforeShuffling()
    {
        SavePlaybackStateCommand command = Command(
            [new(Guid.NewGuid(), false), new(Guid.NewGuid(), false)],
            0,
            _now) with
        { OriginalOrder = [1, 0] };

        await _service.SaveAsync(_userId, command);

        _saved!.OriginalOrder.Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_InvalidState_SavesNothing()
    {
        PlaybackStateSaveStatus status = await _service.SaveAsync(
            _userId,
            Command([new(Guid.NewGuid(), false)], 1, _now));

        status.Should().Be(PlaybackStateSaveStatus.InvalidCurrentIndex);
        await _states.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Test]
    public async Task SaveAsync_NewerStateOfAnotherDevice_IsOutdated()
    {
        _states.SaveAsync(Arg.Any<PlaybackState>(), Arg.Any<CancellationToken>()).Returns(false);

        PlaybackStateSaveStatus status = await _service.SaveAsync(
            _userId,
            Command([new(Guid.NewGuid(), false)], 0, _now.AddMinutes(-1)));

        status.Should().Be(PlaybackStateSaveStatus.Outdated);
    }

    [Test]
    public async Task SaveAsync_UserDeleted_PassesTheErrorOn()
    {
        _states.SaveAsync(Arg.Any<PlaybackState>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<bool>(new UserNotFoundException(_userId)));

        Func<Task> save = () => _service.SaveAsync(_userId, Command([], -1, _now));

        (await save.Should().ThrowAsync<UserNotFoundException>()).Which.UserId.Should().Be(_userId);
    }

    [Test]
    public async Task GetAsync_NothingSaved_ReturnsNull()
    {
        _states.FindAsync(_userId, Arg.Any<CancellationToken>()).Returns((PlaybackState?)null);

        PlaybackStateView? view = await _service.GetAsync(_userId);

        view.Should().BeNull();
    }

    [Test]
    public async Task GetAsync_SavedState_ReturnsItWithItsTracks()
    {
        TrackRow first = TrackRowOf("First");
        TrackRow second = TrackRowOf("Second");
        var playlist = Guid.NewGuid();
        PlaybackState state = State([first.Id, second.Id, first.Id], currentIndex: 1);
        state.AutoplayPositions = [2];
        state.Shuffle = true;
        state.OriginalOrder = [2, 1, 0];
        state.Repeat = RepeatMode.All;
        state.SourceKind = PlaybackSourceKind.Playlist;
        state.SourceId = playlist;
        Saved(state, first, second);

        PlaybackStateView? view = await _service.GetAsync(_userId);

        view.Should().NotBeNull();
        view!.Items.Should().Equal(
            new PlaybackStateEntry(first, false),
            new PlaybackStateEntry(second, false),
            new PlaybackStateEntry(first, true));
        view.CurrentIndex.Should().Be(1);
        view.PositionMilliseconds.Should().Be(30_000);
        view.Shuffle.Should().BeTrue();
        view.OriginalOrder.Should().Equal(2, 1, 0);
        view.Repeat.Should().Be(RepeatMode.All);
        view.Source.Should().Be(new PlaybackSource(PlaybackSourceKind.Playlist, playlist));
        view.DeviceId.Should().Be("phone");
        view.DeviceName.Should().Be("Safari · iPhone");
        view.ChangedAt.Should().Be(_now);
    }

    [Test]
    public async Task GetAsync_TracksLeftTheLibrary_LeavesTheirEntriesOut()
    {
        TrackRow first = TrackRowOf("First");
        TrackRow third = TrackRowOf("Third");
        PlaybackState state = State([first.Id, Guid.NewGuid(), third.Id], currentIndex: 2);
        state.AutoplayPositions = [2];
        Saved(state, first, third);

        PlaybackStateView? view = await _service.GetAsync(_userId);

        view!.Items.Should().Equal(new PlaybackStateEntry(first, false), new PlaybackStateEntry(third, true));
        view.CurrentIndex.Should().Be(1);
        view.PositionMilliseconds.Should().Be(30_000);
    }

    [Test]
    public async Task GetAsync_CurrentTrackLeftTheLibrary_StartsTheNextEntryAtItsBeginning()
    {
        TrackRow first = TrackRowOf("First");
        TrackRow third = TrackRowOf("Third");
        Saved(State([first.Id, Guid.NewGuid(), third.Id], currentIndex: 1), first, third);

        PlaybackStateView? view = await _service.GetAsync(_userId);

        view!.CurrentIndex.Should().Be(1);
        view.Items[view.CurrentIndex].Track.Should().Be(third);
        view.PositionMilliseconds.Should().Be(0);
    }

    [Test]
    public async Task GetAsync_NotShuffled_ReturnsNoOrderBeforeShuffling()
    {
        TrackRow first = TrackRowOf("First");
        PlaybackState state = State([first.Id], currentIndex: 0);
        state.OriginalOrder = [0];
        Saved(state, first);

        PlaybackStateView? view = await _service.GetAsync(_userId);

        view!.OriginalOrder.Should().BeEmpty();
    }

    [Test]
    public async Task GetAsync_SourceWithoutId_ReturnsNoSource()
    {
        TrackRow first = TrackRowOf("First");
        PlaybackState state = State([first.Id], currentIndex: 0);
        state.SourceKind = PlaybackSourceKind.Album;
        Saved(state, first);

        PlaybackStateView? view = await _service.GetAsync(_userId);

        view!.Source.Should().BeNull();
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task ClearAsync_Always_DeletesTheStateOfTheUser(bool saved)
    {
        _states.DeleteAsync(_userId, Arg.Any<CancellationToken>()).Returns(saved);

        bool cleared = await _service.ClearAsync(_userId);

        cleared.Should().Be(saved);
        await _states.Received(1).DeleteAsync(_userId, Arg.Any<CancellationToken>());
    }

    private void Saved(PlaybackState state, params TrackRow[] rows)
    {
        _states.FindAsync(_userId, Arg.Any<CancellationToken>()).Returns(state);
        _tracks.ListByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), _userId, Arg.Any<CancellationToken>())
            .Returns(rows);
    }

    private PlaybackState State(IEnumerable<Guid> trackIds, int currentIndex)
    {
        return new PlaybackState
        {
            UserId = _userId,
            TrackIds = [.. trackIds],
            CurrentIndex = currentIndex,
            PositionMilliseconds = 30_000,
            Shuffle = false,
            Repeat = RepeatMode.Off,
            DeviceId = "phone",
            DeviceName = "Safari · iPhone",
            ChangedAt = _now,
        };
    }

    private static SavePlaybackStateCommand Command(
        IReadOnlyList<PlaybackStateItem> items,
        int currentIndex,
        DateTimeOffset? changedAt)
    {
        return new SavePlaybackStateCommand(
            items,
            currentIndex,
            0,
            false,
            [],
            RepeatMode.Off,
            null,
            "phone",
            "Safari · iPhone",
            changedAt);
    }

    private static TrackRow TrackRowOf(string title)
    {
        return new TrackRow(
            Guid.NewGuid(),
            title,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            TimeSpan.FromMinutes(3),
            true,
            false,
            false,
            _now);
    }
}
