using AwesomeAssertions;
using WaveMP.Core.Devices.Commands;
using WaveMP.Core.Devices.Coordination;
using WaveMP.Core.Devices.Devices;
using WaveMP.Core.Devices.Playbacks;
using WaveMP.Core.Devices.Tests.Fakes;
using WaveMP.Core.Devices.Validations;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Core.Devices.Tests.Coordination;

[TestFixture]
public class DeviceCoordinatorRegistrationTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private DeviceTestContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        _context = new DeviceTestContext();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
    }

    [Test]
    public async Task RegisterAsync_FirstDevice_ReturnsTheDevicesAndAnEmptyPlaybackWithTheStartVersion()
    {
        DeviceConnection connection = new(_userId, Guid.NewGuid(), "c1");

        RegisterResult result = await _context.Coordinator.RegisterAsync(
            connection,
            new RegisterDeviceCommand("laptop", "  Firefox · Windows  ", DeviceType.Desktop, true));

        result.IsRegistered.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        DeviceSessionSnapshot snapshot = result.Snapshot!;
        snapshot.DeviceId.Should().Be("laptop");
        snapshot.Devices.Should().ContainSingle().Which.Should().BeEquivalentTo(new DeviceView(
            "laptop",
            "Firefox · Windows",
            "Firefox · Windows",
            null,
            DeviceType.Desktop,
            true,
            true,
            false,
            DeviceTestContext.Start));
        snapshot.Playback.Version.Should().Be(DeviceTestContext.InitialVersion);
        snapshot.Playback.QueueVersion.Should().Be(DeviceTestContext.InitialVersion);
        snapshot.Playback.ActiveDeviceId.Should().BeNull();
        snapshot.Playback.Status.Should().Be(LivePlaybackStatus.Idle);
        snapshot.Playback.QueueLength.Should().Be(0);
        snapshot.Playback.CurrentIndex.Should().Be(-1);
        snapshot.Playback.Volume.Should().Be(1);
        (await _context.Repository.FindAsync(_userId, "laptop"))!.ReportedName.Should().Be("Firefox · Windows");
    }

    [TestCase("", "Phone", "deviceId", DeviceValidationCode.Required)]
    [TestCase("bad id", "Phone", "deviceId", DeviceValidationCode.InvalidFormat)]
    [TestCase("phone", "   ", "name", DeviceValidationCode.Required)]
    [TestCase("phone", "Bad\u0007Name", "name", DeviceValidationCode.InvalidFormat)]
    public async Task RegisterAsync_InvalidDevice_ReturnsTheErrorAndRegistersNothing(
        string deviceId,
        string name,
        string field,
        DeviceValidationCode code)
    {
        RegisterResult result = await _context.Coordinator.RegisterAsync(
            new DeviceConnection(_userId, Guid.NewGuid(), "c1"),
            new RegisterDeviceCommand(deviceId, name, DeviceType.Phone, false));

        result.IsRegistered.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().BeEquivalentTo(new { Field = field, Code = code });
        (await _context.Repository.ListAsync(_userId)).Should().BeEmpty();
    }

    [Test]
    public async Task RegisterAsync_TooLongNameAndUndefinedType_ReturnsBothErrors()
    {
        RegisterResult result = await _context.Coordinator.RegisterAsync(
            new DeviceConnection(_userId, Guid.NewGuid(), "c1"),
            new RegisterDeviceCommand("phone", new string('x', 101), (DeviceType)42, false));

        result.Errors.Should().BeEquivalentTo(
        [
            new DeviceValidationError("name", DeviceValidationCode.MaxLength, Max: 100),
            new DeviceValidationError("type", DeviceValidationCode.InvalidValue),
        ]);
    }

    [Test]
    public async Task RegisterAsync_SecondDevice_SendsTheNewListToTheOtherDevicesOnly()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        _context.Notifier.Clear();

        DeviceConnection phone = await _context.RegisterAsync(_userId, "phone");

        IReadOnlyList<DeviceView> list = _context.Notifier.Of<IReadOnlyList<DeviceView>>(laptop.ConnectionId).Single();
        list.Select(device => device.Id).Should().BeEquivalentTo(["laptop", "phone"]);
        list.Should().OnlyContain(device => device.Online);
        _context.Notifier.Of<IReadOnlyList<DeviceView>>(phone.ConnectionId).Should().BeEmpty();
    }

    [Test]
    public async Task RegisterAsync_SameDeviceOnANewConnection_ReplacesTheOlderConnection()
    {
        DeviceConnection older = await _context.RegisterAsync(_userId, "laptop", remoteControl: true);
        Guid track = _context.AddTrack("Song");

        DeviceConnection newer = await _context.RegisterAsync(_userId, "laptop");

        _context.Notifier.Replaced.Should().Equal(older.ConnectionId);
        (await _context.PlayAsync(older, [track])).Status.Should().Be(PublishStatus.NotRegistered);
        (await _context.PlayAsync(newer, [track])).Status.Should().Be(PublishStatus.Accepted);
        IReadOnlyList<DeviceView> devices = (await _context.Service.ListAsync(_userId, 0, 10)).Items;
        devices.Should().ContainSingle().Which.RemoteControl.Should().BeFalse();
    }

    [Test]
    public async Task RegisterAsync_SameDeviceIdOfAnotherProfile_IsAnotherDeviceThatSeesNothingOfTheFirst()
    {
        var otherUser = Guid.NewGuid();
        DeviceConnection mine = await _context.RegisterAsync(_userId, "shared", remoteControl: true);
        _context.Notifier.Clear();

        DeviceConnection connection = new(otherUser, Guid.NewGuid(), "other");
        RegisterResult other = await _context.Coordinator.RegisterAsync(
            connection,
            new RegisterDeviceCommand("shared", "Other", DeviceType.Phone, true));

        _context.Notifier.Replaced.Should().BeEmpty();
        _context.Notifier.Messages.Should().BeEmpty();
        other.Snapshot!.Devices.Should().ContainSingle().Which.Name.Should().Be("Other");
        (await _context.Service.ListAsync(_userId, 0, 10)).Items.Should().ContainSingle()
            .Which.Name.Should().Be("Device shared");
        (await _context.Coordinator.SetRemoteControlAsync(mine, false)).Status.Should().Be(CommandStatus.Accepted);
        _context.Notifier.Of<IReadOnlyList<DeviceView>>(connection.ConnectionId).Should().BeEmpty();
    }

    [Test]
    public async Task RegisterAsync_ConnectionRegistersAnotherId_ClosesTheFirstDevice()
    {
        DeviceConnection connection = await _context.RegisterAsync(_userId, "first", connectionId: "c1");

        await _context.Coordinator.RegisterAsync(
            connection,
            new RegisterDeviceCommand("second", "Second", DeviceType.Phone, false));

        IReadOnlyList<DeviceView> devices = (await _context.Service.ListAsync(_userId, 0, 10)).Items;
        devices.Single(device => device.Id == "first").Online.Should().BeFalse();
        devices.Single(device => device.Id == "second").Online.Should().BeTrue();
    }

    [Test]
    public async Task RegisterAsync_MoreThanTheLimit_ForgetsTheLeastRecentlyActiveClosedDevices()
    {
        for (int index = 0; index < DeviceLimits.MaxDevicesPerProfile; index++)
        {
            await _context.Repository.RegisterAsync(
                _userId,
                $"old-{index:D2}",
                "Old",
                DeviceType.Desktop,
                DeviceTestContext.Start.AddDays(-100 + index));
        }

        await _context.RegisterAsync(_userId, "new");

        IReadOnlyList<Device> stored = await _context.Repository.ListAsync(_userId);
        stored.Should().HaveCount(DeviceLimits.MaxDevicesPerProfile);
        stored.Select(device => device.DeviceId).Should().Contain("new").And.NotContain("old-00");
    }

    [Test]
    public async Task RegisterAsync_DeletedProfile_ThrowsUserNotFound()
    {
        _context.Repository.MissingUsers.Add(_userId);

        Func<Task> register = () => _context.RegisterAsync(_userId, "phone");

        await register.Should().ThrowAsync<UserNotFoundException>();
    }

    [Test]
    public async Task DisconnectAsync_RegisteredDevice_ClosesItNotesItsActivityAndTellsTheOthers()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        DeviceConnection phone = await _context.RegisterAsync(_userId, "phone");
        _context.Notifier.Clear();
        _context.Clock.Advance(TimeSpan.FromMinutes(5));

        await _context.Coordinator.DisconnectAsync(phone);

        IReadOnlyList<DeviceView> list = _context.Notifier.Of<IReadOnlyList<DeviceView>>(laptop.ConnectionId).Single();
        list.Single(device => device.Id == "phone").Online.Should().BeFalse();
        (await _context.Repository.FindAsync(_userId, "phone"))!.LastActiveAt
            .Should().Be(DeviceTestContext.Start.AddMinutes(5));
    }

    [Test]
    public async Task DisconnectAsync_ReplacedConnection_KeepsTheDeviceOpen()
    {
        DeviceConnection older = await _context.RegisterAsync(_userId, "laptop");
        await _context.RegisterAsync(_userId, "laptop");

        await _context.Coordinator.DisconnectAsync(older);

        (await _context.Service.ListAsync(_userId, 0, 10)).Items.Single().Online.Should().BeTrue();
    }

    [Test]
    public async Task SetRemoteControlAsync_Allowed_TellsAllDevices()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        DeviceConnection phone = await _context.RegisterAsync(_userId, "phone");
        _context.Notifier.Clear();

        CommandResult result = await _context.Coordinator.SetRemoteControlAsync(laptop, true);

        result.Status.Should().Be(CommandStatus.Accepted);
        _context.Notifier.Of<IReadOnlyList<DeviceView>>(phone.ConnectionId).Single()
            .Single(device => device.Id == "laptop").RemoteControl.Should().BeTrue();
        _context.Notifier.Of<IReadOnlyList<DeviceView>>(laptop.ConnectionId).Should().ContainSingle();
    }

    [Test]
    public async Task SetRemoteControlAsync_Unchanged_SendsNothing()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop", remoteControl: true);
        _context.Notifier.Clear();

        await _context.Coordinator.SetRemoteControlAsync(laptop, true);

        _context.Notifier.Messages.Should().BeEmpty();
    }

    [Test]
    public async Task SetRemoteControlAsync_NotRegistered_ReturnsNotRegistered()
    {
        await _context.RegisterAsync(_userId, "laptop");

        CommandResult result = await _context.Coordinator.SetRemoteControlAsync(
            new DeviceConnection(_userId, Guid.NewGuid(), "unknown"),
            true);

        result.Status.Should().Be(CommandStatus.NotRegistered);
    }

    [Test]
    public async Task AbortSessionAsync_EndedSession_ClosesOnlyItsConnections()
    {
        var ended = Guid.NewGuid();
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop", sessionId: ended);
        DeviceConnection phone = await _context.RegisterAsync(_userId, "phone");
        DeviceConnection other = await _context.RegisterAsync(Guid.NewGuid(), "tablet");

        await _context.Coordinator.AbortSessionAsync(ended);

        _context.Notifier.Aborted.Should().Equal(laptop.ConnectionId);
        (await _context.Coordinator.SetRemoteControlAsync(laptop, true)).Status.Should().Be(
            CommandStatus.NotRegistered);
        (await _context.Coordinator.SetRemoteControlAsync(phone, true)).Status.Should().Be(CommandStatus.Accepted);
        (await _context.Coordinator.SetRemoteControlAsync(other, true)).Status.Should().Be(CommandStatus.Accepted);
        (await _context.Service.ListAsync(_userId, 0, 10)).Items.Single(device => device.Id == "laptop")
            .Online.Should().BeFalse();
    }

    [Test]
    public async Task AbortUserAsync_DeletedProfile_ClosesAllItsConnections()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        DeviceConnection phone = await _context.RegisterAsync(_userId, "phone");
        await _context.RegisterAsync(Guid.NewGuid(), "tablet");

        await _context.Coordinator.AbortUserAsync(_userId);

        _context.Notifier.Aborted.Should().BeEquivalentTo([laptop.ConnectionId, phone.ConnectionId]);
    }

    [Test]
    public async Task GetConnectedSessionIdsAsync_Always_ReturnsTheSessionsOfAllRegisteredConnections()
    {
        DeviceConnection first = await _context.RegisterAsync(_userId, "laptop");
        DeviceConnection second = await _context.RegisterAsync(Guid.NewGuid(), "phone");
        DeviceConnection closed = await _context.RegisterAsync(_userId, "tablet");
        await _context.Coordinator.DisconnectAsync(closed);

        IReadOnlyCollection<Guid> sessionIds = await _context.Coordinator.GetConnectedSessionIdsAsync();

        sessionIds.Should().BeEquivalentTo([first.SessionId, second.SessionId]);
    }

    [Test]
    public async Task RemoveIdleSessionsAsync_LastDeviceClosedLongAgo_ForgetsTheLiveState()
    {
        Guid track = _context.AddTrack("Song");
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        await _context.PlayAsync(laptop, [track]);
        await _context.Coordinator.DisconnectAsync(laptop);
        _context.Clock.Advance(DeviceLimits.IdleSessionLifetime);

        await _context.Coordinator.RemoveIdleSessionsAsync();

        RegisterResult again = await _context.Coordinator.RegisterAsync(
            new DeviceConnection(_userId, Guid.NewGuid(), "again"),
            new RegisterDeviceCommand("laptop", "Laptop", DeviceType.Desktop, false));
        again.Snapshot!.Playback.ActiveDeviceId.Should().BeNull();
        again.Snapshot.Playback.QueueLength.Should().Be(0);
        again.Snapshot.Playback.Version.Should().Be(_context.Clock.GetUtcNow().ToUnixTimeMilliseconds());
    }

    [Test]
    public async Task RemoveIdleSessionsAsync_LastDeviceClosedRecently_KeepsTheLiveState()
    {
        Guid track = _context.AddTrack("Song");
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        await _context.PlayAsync(laptop, [track]);
        await _context.Coordinator.DisconnectAsync(laptop);
        _context.Clock.Advance(DeviceLimits.IdleSessionLifetime - TimeSpan.FromSeconds(1));

        await _context.Coordinator.RemoveIdleSessionsAsync();

        RegisterResult again = await _context.Coordinator.RegisterAsync(
            new DeviceConnection(_userId, Guid.NewGuid(), "again"),
            new RegisterDeviceCommand("laptop", "Laptop", DeviceType.Desktop, false));
        again.Snapshot!.Playback.ActiveDeviceId.Should().Be("laptop");
        again.Snapshot.Playback.QueueLength.Should().Be(1);
    }
}
