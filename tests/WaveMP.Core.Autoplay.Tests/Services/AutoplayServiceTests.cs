using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Autoplay.Selections;
using WaveMP.Core.Autoplay.Services;
using WaveMP.Core.Users.Settings;
using WaveMP.Data.Entities;
using WaveMP.Data.Projections;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Autoplay.Tests.Services;

[TestFixture]
public class AutoplayServiceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private IUserSettingsService _settings = null!;
    private IAutoplaySelector _selector = null!;
    private ITrackRepository _tracks = null!;
    private AutoplayService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _settings = Substitute.For<IUserSettingsService>();
        _selector = Substitute.For<IAutoplaySelector>();
        _tracks = Substitute.For<ITrackRepository>();
        _service = new AutoplayService(_settings, _selector, _tracks);
    }

    [Test]
    public async Task SelectAsync_UnknownUser_ReturnsNull()
    {
        _settings.GetAsync(_userId, Arg.Any<CancellationToken>()).Returns((UserSettings?)null);

        IReadOnlyList<TrackRow>? result = await _service.SelectAsync(_userId, Command([Guid.NewGuid()]));

        result.Should().BeNull();
        await _selector.DidNotReceiveWithAnyArgs().SelectAsync(default!, default);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task SelectAsync_Always_FollowsTheVideoOnlySettingOfTheUser(bool includeVideoOnly)
    {
        GivenSettings(new UserSettings { UserId = _userId, AutoplayIncludeVideoOnly = includeVideoOnly });
        var listed = Guid.NewGuid();
        var excluded = Guid.NewGuid();

        await _service.SelectAsync(_userId, Command([listed], [excluded], 7));

        await _selector.Received(1).SelectAsync(
            Arg.Is<AutoplayRequest>(request =>
                request.UserId == _userId
                && request.IncludeVideoOnly == includeVideoOnly
                && request.Count == 7
                && request.ListTrackIds.SequenceEqual(new[] { listed })
                && request.ExcludedTrackIds.SequenceEqual(new[] { excluded })),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SelectAsync_LongList_PassesItsLastTracks()
    {
        GivenSettings(new UserSettings { UserId = _userId });
        Guid[] list = [.. Enumerable.Range(0, AutoplayLimits.MaxListTracks + 20).Select(_ => Guid.NewGuid())];

        await _service.SelectAsync(_userId, Command(list));

        await _selector.Received(1).SelectAsync(
            Arg.Is<AutoplayRequest>(request => request.ListTrackIds.SequenceEqual(list.Skip(20))),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SelectAsync_ChosenSongs_ReturnsThemInTheOrderOfTheChoiceWithoutRemovedTracks()
    {
        GivenSettings(new UserSettings { UserId = _userId });
        TrackRow first = Row("First");
        TrackRow second = Row("Second");
        var removed = Guid.NewGuid();
        _selector.SelectAsync(default!, default).ReturnsForAnyArgs([second.Id, removed, first.Id]);
        _tracks.ListByIdsAsync(default!, default, default).ReturnsForAnyArgs([first, second]);

        IReadOnlyList<TrackRow>? result = await _service.SelectAsync(_userId, Command([Guid.NewGuid()]));

        result.Should().Equal(second, first);
        await _tracks.Received(1).ListByIdsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 3),
            _userId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SelectAsync_NothingChosen_ReturnsAnEmptyList()
    {
        GivenSettings(new UserSettings { UserId = _userId });
        _selector.SelectAsync(default!, default).ReturnsForAnyArgs([]);

        IReadOnlyList<TrackRow>? result = await _service.SelectAsync(_userId, Command([Guid.NewGuid()]));

        result.Should().BeEmpty();
        await _tracks.DidNotReceiveWithAnyArgs().ListByIdsAsync(default!, default, default);
    }

    [TestCase(0)]
    [TestCase(AutoplayLimits.MaxCount + 1)]
    public async Task SelectAsync_CountOutOfRange_Throws(int count)
    {
        Func<Task> select = () => _service.SelectAsync(_userId, Command([Guid.NewGuid()], count: count));

        await select.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    private void GivenSettings(UserSettings settings)
    {
        _settings.GetAsync(_userId, Arg.Any<CancellationToken>()).Returns(settings);
        _selector.SelectAsync(default!, default).ReturnsForAnyArgs([]);
    }

    private static AutoplayCommand Command(
        IReadOnlyList<Guid> list,
        IReadOnlyCollection<Guid>? excluded = null,
        int count = AutoplayLimits.DefaultCount)
    {
        return new AutoplayCommand(list, excluded ?? [], count);
    }

    private static TrackRow Row(string title)
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
            null,
            true,
            false,
            false,
            DateTimeOffset.UnixEpoch);
    }
}
