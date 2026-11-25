using AwesomeAssertions;
using WaveMP.Core.Devices.Commands;
using WaveMP.Core.Devices.Coordination;
using WaveMP.Core.Devices.Playbacks;
using WaveMP.Core.Devices.Tests.Fakes;
using WaveMP.Core.Devices.Validations;
using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Devices.Tests.Coordination;

[TestFixture]
public class DeviceCoordinatorCommandTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private DeviceTestContext _context = null!;
    private Guid _first;
    private Guid _second;
    private DeviceConnection _laptop = null!;
    private DeviceConnection _phone = null!;
    private PublishResult _played = null!;

    [SetUp]
    public async Task SetUp()
    {
        _context = new DeviceTestContext();
        _first = _context.AddTrack("First");
        _second = _context.AddTrack("Second");
        _laptop = await _context.RegisterAsync(_userId, "laptop", remoteControl: true);
        _phone = await _context.RegisterAsync(_userId, "phone");
        _played = await _context.PlayAsync(_laptop, [_first, _second]);
        _context.Notifier.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task SendCommandAsync_ToTheActivePermittedDevice_PassesItOn()
    {
        CommandResult result = await _context.Coordinator.SendCommandAsync(
            _phone,
            new DeviceCommand("laptop", DeviceCommandType.SetVolume, Volume: 0.25, Muted: false));

        result.Status.Should().Be(CommandStatus.Accepted);
        result.Version.Should().Be(_played.Version);
        ReceivedCommand received = _context.Notifier.Of<ReceivedCommand>(_laptop.ConnectionId).Single();
        received.Should().BeEquivalentTo(new
        {
            FromDeviceId = "phone",
            Type = DeviceCommandType.SetVolume,
            _played.Version,
            Volume = (double?)0.25,
            Muted = (bool?)false,
        });
        _context.Notifier.Of<ReceivedCommand>(_phone.ConnectionId).Should().BeEmpty();
    }

    [Test]
    public async Task SendCommandAsync_DeviceWithoutPermission_ReturnsRemoteControlNotAllowedAndPassesNothingOn()
    {
        await _context.Coordinator.SetRemoteControlAsync(_laptop, false);
        _context.Notifier.Clear();

        CommandResult result = await _context.Coordinator.SendCommandAsync(
            _phone,
            new DeviceCommand("laptop", DeviceCommandType.Pause));

        result.Status.Should().Be(CommandStatus.RemoteControlNotAllowed);
        _context.Notifier.Messages.Should().BeEmpty();
    }

    [Test]
    public async Task SendCommandAsync_DeviceOfAnotherProfile_ReturnsDeviceNotFound()
    {
        DeviceConnection foreign = await _context.RegisterAsync(Guid.NewGuid(), "foreign", remoteControl: true);
        await _context.PlayAsync(foreign, [_first]);
        _context.Notifier.Clear();

        CommandResult result = await _context.Coordinator.SendCommandAsync(
            _phone,
            new DeviceCommand("foreign", DeviceCommandType.Pause));

        result.Status.Should().Be(CommandStatus.DeviceNotFound);
        _context.Notifier.Messages.Should().BeEmpty();
    }

    [Test]
    public async Task SendCommandAsync_FromAnotherProfileToTheSameDeviceId_ReturnsDeviceNotFound()
    {
        DeviceConnection intruder = await _context.RegisterAsync(Guid.NewGuid(), "intruder");
        _context.Notifier.Clear();

        CommandResult result = await _context.Coordinator.SendCommandAsync(
            intruder,
            new DeviceCommand("laptop", DeviceCommandType.Pause));

        result.Status.Should().Be(CommandStatus.DeviceNotFound);
        _context.Notifier.Messages.Should().BeEmpty();
    }

    [Test]
    public async Task SendCommandAsync_ClosedDevice_ReturnsDeviceNotFound()
    {
        await _context.Coordinator.DisconnectAsync(_laptop);

        CommandResult result = await _context.Coordinator.SendCommandAsync(
            _phone,
            new DeviceCommand("laptop", DeviceCommandType.Play));

        result.Status.Should().Be(CommandStatus.DeviceNotFound);
    }

    [Test]
    public async Task SendCommandAsync_PermittedDeviceThatDoesNotPlay_ReturnsNotActive()
    {
        DeviceConnection tablet = await _context.RegisterAsync(_userId, "tablet", remoteControl: true);
        _context.Notifier.Clear();

        CommandResult result = await _context.Coordinator.SendCommandAsync(
            _phone,
            new DeviceCommand("tablet", DeviceCommandType.Play));

        result.Status.Should().Be(CommandStatus.NotActive);
        _context.Notifier.Of<ReceivedCommand>(tablet.ConnectionId).Should().BeEmpty();
    }

    [Test]
    public async Task SendCommandAsync_ToItself_ReturnsInvalid()
    {
        CommandResult result = await _context.Coordinator.SendCommandAsync(
            _laptop,
            new DeviceCommand("laptop", DeviceCommandType.Pause));

        result.Status.Should().Be(CommandStatus.Invalid);
        result.Errors.Should().ContainSingle().Which.Should().Be(
            new DeviceValidationError("targetDeviceId", DeviceValidationCode.InvalidValue));
    }

    [Test]
    public async Task SendCommandAsync_NotRegistered_ReturnsNotRegistered()
    {
        CommandResult result = await _context.Coordinator.SendCommandAsync(
            new DeviceConnection(_userId, Guid.NewGuid(), "unknown"),
            new DeviceCommand("laptop", DeviceCommandType.Pause));

        result.Status.Should().Be(CommandStatus.NotRegistered);
    }

    [Test]
    public async Task SendCommandAsync_InvalidCommand_ReturnsTheErrorsWithoutLookingAtTheDevices()
    {
        CommandResult result = await _context.Coordinator.SendCommandAsync(
            _phone,
            new DeviceCommand("laptop", DeviceCommandType.Seek, PositionMilliseconds: -5));

        result.Status.Should().Be(CommandStatus.Invalid);
        result.Errors.Should().ContainSingle().Which.Field.Should().Be("positionMilliseconds");
        _context.Notifier.Messages.Should().BeEmpty();
    }

    [Test]
    public async Task SendCommandAsync_QueueEntryWithTheCurrentQueueVersion_PassesItOn()
    {
        CommandResult result = await _context.Coordinator.SendCommandAsync(
            _phone,
            new DeviceCommand(
                "laptop",
                DeviceCommandType.MoveQueueEntry,
                ExpectedQueueVersion: _played.QueueVersion,
                Index: 1,
                ToIndex: 0));

        result.Status.Should().Be(CommandStatus.Accepted);
        _context.Notifier.Of<ReceivedCommand>(_laptop.ConnectionId).Single().Should().BeEquivalentTo(new
        {
            Type = DeviceCommandType.MoveQueueEntry,
            ExpectedQueueVersion = (long?)_played.QueueVersion,
            Index = (int?)1,
            ToIndex = (int?)0,
        });
    }

    [Test]
    public async Task SendCommandAsync_QueueChangedSinceTheRemoteSawIt_ReturnsConflictAndPassesNothingOn()
    {
        await _context.PlayAsync(_laptop, [_second], claim: false);
        _context.Notifier.Clear();

        CommandResult result = await _context.Coordinator.SendCommandAsync(
            _phone,
            new DeviceCommand(
                "laptop",
                DeviceCommandType.RemoveQueueEntry,
                ExpectedQueueVersion: _played.QueueVersion,
                Index: 1));

        result.Status.Should().Be(CommandStatus.Conflict);
        result.QueueVersion.Should().Be(_played.Version + 1);
        _context.Notifier.Messages.Should().BeEmpty();
    }

    [Test]
    public async Task SendCommandAsync_QueueEntryOutsideTheQueue_ReturnsOutOfRange()
    {
        CommandResult result = await _context.Coordinator.SendCommandAsync(
            _phone,
            new DeviceCommand(
                "laptop",
                DeviceCommandType.MoveQueueEntry,
                ExpectedQueueVersion: _played.QueueVersion,
                Index: 2,
                ToIndex: 5));

        result.Status.Should().Be(CommandStatus.Invalid);
        result.Errors.Should().BeEquivalentTo(
        [
            new DeviceValidationError("index", DeviceValidationCode.OutOfRange, 0, 1),
            new DeviceValidationError("toIndex", DeviceValidationCode.OutOfRange, 0, 1),
        ]);
    }

    [Test]
    public async Task SendCommandAsync_PlayTracks_PassesTheKnownSongsOnAndTheStartFollows()
    {
        var unknown = Guid.NewGuid();
        PlaybackSource source = new(PlaybackSourceKind.Album, Guid.NewGuid());

        CommandResult result = await _context.Coordinator.SendCommandAsync(
            _phone,
            new DeviceCommand(
                "laptop",
                DeviceCommandType.PlayTracks,
                TrackIds: [unknown, _first, _second],
                StartIndex: 2,
                Source: source,
                Shuffle: false));

        result.Status.Should().Be(CommandStatus.Accepted);
        ReceivedCommand received = _context.Notifier.Of<ReceivedCommand>(_laptop.ConnectionId).Single();
        received.Tracks!.Select(track => track.Title).Should().Equal("First", "Second");
        received.StartIndex.Should().Be(1);
        received.Source.Should().Be(source);
        received.Shuffle.Should().BeFalse();
    }

    [Test]
    public async Task SendCommandAsync_PlayTracksStartingAtAnUnknownSong_StartsAtTheNextKnownSong()
    {
        var unknown = Guid.NewGuid();

        await _context.Coordinator.SendCommandAsync(
            _phone,
            new DeviceCommand("laptop", DeviceCommandType.PlayTracks, TrackIds: [_first, unknown, _second], StartIndex: 1));

        _context.Notifier.Of<ReceivedCommand>(_laptop.ConnectionId).Single().StartIndex.Should().Be(1);
    }

    [Test]
    public async Task SendCommandAsync_AddToQueue_PassesTheSongsOnWithDuplicates()
    {
        await _context.Coordinator.SendCommandAsync(
            _phone,
            new DeviceCommand("laptop", DeviceCommandType.AddToQueue, TrackIds: [_second, _second], StartIndex: 1));

        ReceivedCommand received = _context.Notifier.Of<ReceivedCommand>(_laptop.ConnectionId).Single();
        received.Tracks!.Select(track => track.Id).Should().Equal(_second, _second);
        received.StartIndex.Should().BeNull();
    }

    [Test]
    public async Task SendCommandAsync_OnlyUnknownSongs_ReturnsInvalid()
    {
        CommandResult result = await _context.Coordinator.SendCommandAsync(
            _phone,
            new DeviceCommand("laptop", DeviceCommandType.PlayNext, TrackIds: [Guid.NewGuid()]));

        result.Status.Should().Be(CommandStatus.Invalid);
        result.Errors.Should().ContainSingle().Which.Should().Be(
            new DeviceValidationError("trackIds", DeviceValidationCode.InvalidValue));
        _context.Notifier.Messages.Should().BeEmpty();
    }

    [Test]
    public async Task SendCommandAsync_ManySendersAtOnce_PassesEveryCommandOnInOneVersion()
    {
        DeviceConnection tablet = await _context.RegisterAsync(_userId, "tablet");
        _context.Notifier.Clear();

        CommandResult[] results = await Task.WhenAll(Enumerable.Range(0, 20).Select(index => Task.Run(() =>
            _context.Coordinator.SendCommandAsync(
                index % 2 == 0 ? _phone : tablet,
                new DeviceCommand("laptop", DeviceCommandType.Seek, PositionMilliseconds: index * 1000)))));

        results.Should().OnlyContain(result => result.Status == CommandStatus.Accepted);
        _context.Notifier.Of<ReceivedCommand>(_laptop.ConnectionId).Should().HaveCount(20);
    }
}
