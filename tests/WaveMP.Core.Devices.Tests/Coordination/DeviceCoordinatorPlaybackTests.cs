using AwesomeAssertions;
using NSubstitute;
using WaveMP.Core.Devices.Commands;
using WaveMP.Core.Devices.Coordination;
using WaveMP.Core.Devices.Devices;
using WaveMP.Core.Devices.Playbacks;
using WaveMP.Core.Devices.Tests.Fakes;
using WaveMP.Core.Devices.Validations;
using WaveMP.Core.Streaming.PlaybackStates;
using WaveMP.Data.Entities;

namespace WaveMP.Core.Devices.Tests.Coordination;

[TestFixture]
public class DeviceCoordinatorPlaybackTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private DeviceTestContext _context = null!;
    private Guid _first;
    private Guid _second;

    [SetUp]
    public void SetUp()
    {
        _context = new DeviceTestContext();
        _first = _context.AddTrack("First");
        _second = _context.AddTrack("Second");
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task PublishAsync_WithoutActiveDeviceAndWithoutClaim_ReturnsNotActive()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");

        PublishResult result = await _context.PlayAsync(laptop, [_first], claim: false);

        result.Status.Should().Be(PublishStatus.NotActive);
        result.Version.Should().Be(DeviceTestContext.InitialVersion);
    }

    [Test]
    public async Task PublishAsync_Claim_MakesTheDeviceActiveAndTellsTheOthers()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        DeviceConnection phone = await _context.RegisterAsync(_userId, "phone");
        _context.Notifier.Clear();

        PublishResult result = await _context.PlayAsync(
            laptop,
            [_first, _second],
            currentIndex: 1,
            positionMilliseconds: 5_000);

        result.Status.Should().Be(PublishStatus.Accepted);
        result.Version.Should().Be(DeviceTestContext.InitialVersion + 1);
        result.QueueVersion.Should().Be(DeviceTestContext.InitialVersion + 1);
        LivePlaybackView view = _context.Notifier.Of<LivePlaybackView>(phone.ConnectionId).Single();
        view.Should().BeEquivalentTo(new
        {
            Version = DeviceTestContext.InitialVersion + 1,
            ActiveDeviceId = "laptop",
            Status = LivePlaybackStatus.Playing,
            CurrentIndex = 1,
            CurrentTrackId = (Guid?)_second,
            QueueLength = 2,
            PositionMilliseconds = 5_000,
            DurationMilliseconds = 180_000,
            Volume = 0.8,
        });
        _context.Notifier.Of<LivePlaybackView>(laptop.ConnectionId).Should().BeEmpty();
        _context.Notifier.Of<IReadOnlyList<DeviceView>>(phone.ConnectionId).Single()
            .Single(device => device.Active).Id.Should().Be("laptop");
    }

    [Test]
    public async Task PublishAsync_ActiveDeviceWithoutQueue_KeepsTheQueueAndItsVersion()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        PublishResult first = await _context.PlayAsync(laptop, [_first, _second]);

        PublishResult second = await _context.Coordinator.PublishAsync(
            laptop,
            new PublishPlaybackCommand(false, DeviceTestContext.Playback([_first, _second], 1) with { Queue = null }));

        second.Status.Should().Be(PublishStatus.Accepted);
        second.Version.Should().Be(first.Version + 1);
        second.QueueVersion.Should().Be(first.QueueVersion);
        DeviceQueueView queue = (await _context.Coordinator.GetQueueAsync(laptop)).Queue!;
        queue.Items.Select(item => item.TrackId).Should().Equal(_first, _second);
        queue.CurrentIndex.Should().Be(1);
    }

    [Test]
    public async Task PublishAsync_SameQueueAgain_KeepsTheQueueVersion()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        PublishResult first = await _context.PlayAsync(laptop, [_first, _second]);

        PublishResult second = await _context.PlayAsync(laptop, [_first, _second], currentIndex: 1, claim: false);

        second.QueueVersion.Should().Be(first.QueueVersion);
    }

    [Test]
    public async Task PublishAsync_ChangedQueue_RaisesTheQueueVersion()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        await _context.PlayAsync(laptop, [_first]);

        PublishResult second = await _context.PlayAsync(laptop, [_first, _second], claim: false);

        second.QueueVersion.Should().Be(second.Version);
    }

    [Test]
    public async Task PublishAsync_AnotherDeviceWithoutClaim_ReturnsNotActiveAndKeepsTheState()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        DeviceConnection phone = await _context.RegisterAsync(_userId, "phone");
        PublishResult played = await _context.PlayAsync(laptop, [_first]);

        PublishResult result = await _context.PlayAsync(phone, [_second], claim: false);

        result.Status.Should().Be(PublishStatus.NotActive);
        result.Version.Should().Be(played.Version);
        (await _context.Coordinator.GetQueueAsync(phone)).Queue!.Items.Single().TrackId.Should().Be(_first);
    }

    [Test]
    public async Task PublishAsync_AnotherDeviceClaims_BecomesTheActiveDevice()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        DeviceConnection phone = await _context.RegisterAsync(_userId, "phone");
        await _context.PlayAsync(laptop, [_first]);
        _context.Notifier.Clear();

        PublishResult result = await _context.PlayAsync(phone, [_second]);

        result.Status.Should().Be(PublishStatus.Accepted);
        _context.Notifier.Of<LivePlaybackView>(laptop.ConnectionId).Single().ActiveDeviceId.Should().Be("phone");
    }

    [Test]
    public async Task PublishAsync_InvalidState_ReturnsTheErrorsAndKeepsTheVersion()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        LivePlaybackInput invalid = DeviceTestContext.Playback([_first], currentIndex: 3) with { Volume = 1.5 };

        PublishResult result = await _context.Coordinator.PublishAsync(
            laptop,
            new PublishPlaybackCommand(true, invalid));

        result.Status.Should().Be(PublishStatus.Invalid);
        result.Version.Should().Be(DeviceTestContext.InitialVersion);
        result.Errors.Select(error => error.Field).Should().BeEquivalentTo(
            ["playback.currentIndex", "playback.volume"]);
    }

    [Test]
    public async Task PublishAsync_NotRegistered_ReturnsNotRegistered()
    {
        await _context.RegisterAsync(_userId, "laptop");

        PublishResult result = await _context.PlayAsync(
            new DeviceConnection(_userId, Guid.NewGuid(), "unknown"),
            [_first]);

        result.Status.Should().Be(PublishStatus.NotRegistered);
    }

    [Test]
    public async Task PublishAsync_ProfileWithoutDevices_ReturnsNotRegistered()
    {
        PublishResult result = await _context.PlayAsync(new DeviceConnection(_userId, Guid.NewGuid(), "c1"), [_first]);

        result.Status.Should().Be(PublishStatus.NotRegistered);
    }

    [Test]
    public async Task PlaybackChanged_WhilePlaying_CarriesThePositionAtTheTimeOfSending()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        DeviceConnection phone = await _context.RegisterAsync(_userId, "phone", remoteControl: true);
        await _context.PlayAsync(laptop, [_first], positionMilliseconds: 10_000);
        _context.Clock.Advance(TimeSpan.FromSeconds(4));

        RegisterResult snapshot = await _context.Coordinator.RegisterAsync(
            phone,
            new RegisterDeviceCommand("phone", "Phone", DeviceType.Phone, true));

        snapshot.Snapshot!.Playback.PositionMilliseconds.Should().Be(14_000);
    }

    [Test]
    public async Task GetQueueAsync_SongLeftTheLibrary_ReturnsItsEntryWithoutSong()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        DeviceConnection phone = await _context.RegisterAsync(_userId, "phone");
        var gone = Guid.NewGuid();
        await _context.Coordinator.PublishAsync(
            laptop,
            new PublishPlaybackCommand(true, DeviceTestContext.Playback([_first, gone, _second]) with
            {
                Queue = new LiveQueueInput(
                    [new(_first, false), new(gone, false), new(_second, true)],
                    true,
                    [2, 0, 1],
                    new PlaybackSource(PlaybackSourceKind.Album, Guid.Empty)),
            }));

        DeviceQueueResult result = await _context.Coordinator.GetQueueAsync(phone);

        result.Status.Should().Be(CommandStatus.Accepted);
        DeviceQueueView queue = result.Queue!;
        queue.Shuffle.Should().BeTrue();
        queue.Source.Should().Be(new PlaybackSource(PlaybackSourceKind.Album, Guid.Empty));
        queue.Items.Select(item => item.Position).Should().Equal(0, 1, 2);
        queue.Items.Select(item => item.Track?.Title).Should().Equal("First", null, "Second");
        queue.Items.Select(item => item.Autoplay).Should().Equal(false, false, true);
        await _context.Tracks.Received().ListByIdsAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(),
            _userId,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GetQueueAsync_NotRegistered_ReturnsNotRegistered()
    {
        DeviceQueueResult result = await _context.Coordinator.GetQueueAsync(
            new DeviceConnection(_userId, Guid.NewGuid(), "c1"));

        result.Status.Should().Be(CommandStatus.NotRegistered);
        result.Queue.Should().BeNull();
    }

    [Test]
    public async Task TransferAsync_ToAPermittedDevice_SavesTheStateAtTheCurrentPositionAndMovesPlayback()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop", remoteControl: true);
        DeviceConnection phone = await _context.RegisterAsync(_userId, "phone", remoteControl: true);
        DeviceConnection tablet = await _context.RegisterAsync(_userId, "tablet");
        PublishResult played = await _context.PlayAsync(
            laptop,
            [_first, _second],
            currentIndex: 1,
            positionMilliseconds: 30_000);
        _context.Clock.Advance(TimeSpan.FromSeconds(12));
        _context.Notifier.Clear();

        CommandResult result = await _context.Coordinator.TransferAsync(
            tablet,
            new TransferPlaybackCommand("phone", true, played.Version));

        result.Status.Should().Be(CommandStatus.Accepted);
        result.Version.Should().Be(played.Version + 1);
        await _context.States.Received(1).SaveAsync(
            _userId,
            Arg.Is<SavePlaybackStateCommand>(command =>
                command.Items.Count == 2
                && command.CurrentIndex == 1
                && command.PositionMilliseconds == 42_000
                && command.DeviceId == "phone"
                && command.DeviceName == "Device phone"
                && command.ChangedAt == _context.Clock.GetUtcNow()),
            Arg.Any<CancellationToken>());
        foreach (DeviceConnection connection in new[] { laptop, phone, tablet })
        {
            LivePlaybackView view = _context.Notifier.Of<LivePlaybackView>(connection.ConnectionId).Single();
            view.ActiveDeviceId.Should().Be("phone");
            view.Status.Should().Be(LivePlaybackStatus.Paused);
            view.PositionMilliseconds.Should().Be(42_000);
            view.Handover.Should().Be(new PlaybackHandover("laptop", true));
        }
    }

    [Test]
    public async Task TransferAsync_TargetWithoutPermission_ReturnsRemoteControlNotAllowedAndChangesNothing()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop", remoteControl: true);
        await _context.RegisterAsync(_userId, "phone");
        PublishResult played = await _context.PlayAsync(laptop, [_first]);
        _context.Notifier.Clear();

        CommandResult result = await _context.Coordinator.TransferAsync(
            laptop,
            new TransferPlaybackCommand("phone", true));

        result.Status.Should().Be(CommandStatus.RemoteControlNotAllowed);
        result.Version.Should().Be(played.Version);
        _context.Notifier.Messages.Should().BeEmpty();
        await _context.States.DidNotReceiveWithAnyArgs().SaveAsync(default, default!, default);
    }

    [Test]
    public async Task TransferAsync_AwayFromAnOpenDeviceWithoutPermission_ReturnsRemoteControlNotAllowed()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        DeviceConnection phone = await _context.RegisterAsync(_userId, "phone", remoteControl: true);
        await _context.PlayAsync(laptop, [_first]);

        CommandResult toPhone = await _context.Coordinator.TransferAsync(
            phone,
            new TransferPlaybackCommand("phone", true));

        toPhone.Status.Should().Be(CommandStatus.RemoteControlNotAllowed);
    }

    [Test]
    public async Task TransferAsync_PlayHereWhileTheActiveDeviceIsClosed_MovesPlaybackToTheCaller()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        DeviceConnection phone = await _context.RegisterAsync(_userId, "phone");
        await _context.PlayAsync(laptop, [_first]);
        await _context.Coordinator.DisconnectAsync(laptop);

        CommandResult result = await _context.Coordinator.TransferAsync(
            phone,
            new TransferPlaybackCommand("phone", false));

        result.Status.Should().Be(CommandStatus.Accepted);
        _context.Notifier.Of<LivePlaybackView>(phone.ConnectionId).Last().Should().BeEquivalentTo(new
        {
            ActiveDeviceId = "phone",
            Handover = new PlaybackHandover("laptop", false),
        });
    }

    [Test]
    public async Task TransferAsync_StaleVersion_ReturnsConflict()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop", remoteControl: true);
        DeviceConnection phone = await _context.RegisterAsync(_userId, "phone", remoteControl: true);
        PublishResult played = await _context.PlayAsync(laptop, [_first]);
        await _context.PlayAsync(laptop, [_first], positionMilliseconds: 2_000, claim: false);

        CommandResult result = await _context.Coordinator.TransferAsync(
            phone,
            new TransferPlaybackCommand("phone", true, played.Version));

        result.Status.Should().Be(CommandStatus.Conflict);
        result.Version.Should().Be(played.Version + 1);
    }

    [Test]
    public async Task TransferAsync_TwoAtTheSameTime_AcceptsExactlyOne()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop", remoteControl: true);
        DeviceConnection phone = await _context.RegisterAsync(_userId, "phone", remoteControl: true);
        DeviceConnection tablet = await _context.RegisterAsync(_userId, "tablet", remoteControl: true);
        PublishResult played = await _context.PlayAsync(laptop, [_first]);

        CommandResult[] results = await Task.WhenAll(
            Task.Run(
                () => _context.Coordinator.TransferAsync(
                    phone,
                    new TransferPlaybackCommand("phone", true, played.Version))),
            Task.Run(
                () => _context.Coordinator.TransferAsync(
                    tablet,
                    new TransferPlaybackCommand("tablet", true, played.Version))));

        results.Select(result => result.Status).Should().BeEquivalentTo(
            [CommandStatus.Accepted, CommandStatus.Conflict]);
    }

    [Test]
    public async Task TransferAsync_ToTheActiveDevice_ChangesNothing()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop", remoteControl: true);
        PublishResult played = await _context.PlayAsync(laptop, [_first]);

        CommandResult result = await _context.Coordinator.TransferAsync(
            laptop,
            new TransferPlaybackCommand("laptop", true));

        result.Status.Should().Be(CommandStatus.Accepted);
        result.Version.Should().Be(played.Version);
    }

    [Test]
    public async Task TransferAsync_DeviceOfAnotherProfile_ReturnsDeviceNotFound()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop", remoteControl: true);
        await _context.RegisterAsync(Guid.NewGuid(), "foreign", remoteControl: true);

        CommandResult result = await _context.Coordinator.TransferAsync(
            laptop,
            new TransferPlaybackCommand("foreign", true));

        result.Status.Should().Be(CommandStatus.DeviceNotFound);
    }

    [Test]
    public async Task TransferAsync_InvalidTarget_ReturnsInvalid()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");

        CommandResult result = await _context.Coordinator.TransferAsync(
            laptop,
            new TransferPlaybackCommand("not valid", true));

        result.Status.Should().Be(CommandStatus.Invalid);
        result.Errors.Should().ContainSingle().Which.Code.Should().Be(DeviceValidationCode.InvalidFormat);
    }
}
