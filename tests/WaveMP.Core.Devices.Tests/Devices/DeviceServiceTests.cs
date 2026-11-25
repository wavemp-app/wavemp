using AwesomeAssertions;
using WaveMP.Core.Devices.Coordination;
using WaveMP.Core.Devices.Devices;
using WaveMP.Core.Devices.Tests.Fakes;
using WaveMP.Core.Devices.Validations;
using WaveMP.Data.Entities;
using WaveMP.Data.Queries;

namespace WaveMP.Core.Devices.Tests.Devices;

[TestFixture]
public class DeviceServiceTests
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
    public async Task ListAsync_Always_ListsOpenDevicesFirstThenTheMostRecentlyActive()
    {
        await _context.Repository.RegisterAsync(_userId, "old", "Old", DeviceType.Tv, DeviceTestContext.Start.AddDays(-2));
        await _context.Repository.RegisterAsync(_userId, "recent", "Recent", DeviceType.Tablet, DeviceTestContext.Start.AddDays(-1));
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop", remoteControl: true);
        await _context.PlayAsync(laptop, [_context.AddTrack("Song")]);
        await _context.Repository.RegisterAsync(Guid.NewGuid(), "foreign", "Foreign", DeviceType.Phone, DeviceTestContext.Start);

        Page<DeviceView> page = await _context.Service.ListAsync(_userId, 0, 10);

        page.Total.Should().Be(3);
        page.Items.Select(device => device.Id).Should().Equal("laptop", "recent", "old");
        page.Items[0].Should().BeEquivalentTo(new { Online = true, RemoteControl = true, Active = true });
        page.Items[1].Should().BeEquivalentTo(new { Online = false, RemoteControl = false, Active = false });
    }

    [Test]
    public async Task ListAsync_WithOffsetAndLimit_ReturnsThePage()
    {
        for (int index = 0; index < 5; index++)
        {
            await _context.Repository.RegisterAsync(
                _userId,
                $"device-{index}",
                "Device",
                DeviceType.Desktop,
                DeviceTestContext.Start.AddMinutes(-index));
        }

        Page<DeviceView> page = await _context.Service.ListAsync(_userId, 1, 2);

        page.Should().BeEquivalentTo(new { Offset = 1, Limit = 2, Total = 5 });
        page.Items.Select(device => device.Id).Should().Equal("device-1", "device-2");
    }

    [Test]
    public async Task ListAsync_ClosedDeviceThatPlayedLast_IsStillActiveButWithoutRemoteControl()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop", remoteControl: true);
        await _context.PlayAsync(laptop, [_context.AddTrack("Song")]);
        await _context.Coordinator.DisconnectAsync(laptop);

        DeviceView device = (await _context.Service.ListAsync(_userId, 0, 10)).Items.Single();

        device.Should().BeEquivalentTo(new { Online = false, RemoteControl = false, Active = true });
    }

    [Test]
    public async Task GetAsync_OpenDevice_ReturnsItWithItsLiveState()
    {
        await _context.RegisterAsync(_userId, "laptop", remoteControl: true);

        DeviceView? device = await _context.Service.GetAsync(_userId, "laptop");

        device.Should().BeEquivalentTo(new { Id = "laptop", Online = true, RemoteControl = true, Active = false });
    }

    [Test]
    public async Task GetAsync_DeviceOfAnotherProfile_ReturnsNull()
    {
        await _context.RegisterAsync(Guid.NewGuid(), "laptop");

        DeviceView? device = await _context.Service.GetAsync(_userId, "laptop");

        device.Should().BeNull();
    }

    [Test]
    public async Task RenameAsync_KnownDevice_GivesItTheNameAndTellsTheOpenDevices()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        _context.Notifier.Clear();

        DeviceRenameResult result = await _context.Service.RenameAsync(_userId, "laptop", "  Living room PC ");

        result.Status.Should().Be(DeviceRenameStatus.Renamed);
        result.Device.Should().BeEquivalentTo(new
        {
            Id = "laptop",
            Name = "Living room PC",
            CustomName = "Living room PC",
            ReportedName = "Device laptop",
            Online = true,
        });
        _context.Notifier.Of<IReadOnlyList<DeviceView>>(laptop.ConnectionId).Single().Single().Name
            .Should().Be("Living room PC");
    }

    [Test]
    public async Task RenameAsync_ReportedName_RemovesTheGivenName()
    {
        await _context.RegisterAsync(_userId, "laptop");
        await _context.Service.RenameAsync(_userId, "laptop", "Living room PC");

        DeviceRenameResult result = await _context.Service.RenameAsync(_userId, "laptop", "Device laptop");

        result.Device!.CustomName.Should().BeNull();
        result.Device.Name.Should().Be("Device laptop");
    }

    [Test]
    public async Task RenameAsync_ClosedDevice_RenamesIt()
    {
        await _context.Repository.RegisterAsync(_userId, "phone", "Safari · iPhone", DeviceType.Phone, DeviceTestContext.Start);

        DeviceRenameResult result = await _context.Service.RenameAsync(_userId, "phone", "Kitchen");

        result.Device!.Should().BeEquivalentTo(new { Name = "Kitchen", Online = false });
    }

    [Test]
    public async Task RenameAsync_DeviceOfAnotherProfile_ReturnsNotFound()
    {
        await _context.Repository.RegisterAsync(Guid.NewGuid(), "phone", "Phone", DeviceType.Phone, DeviceTestContext.Start);

        DeviceRenameResult result = await _context.Service.RenameAsync(_userId, "phone", "Mine");

        result.Status.Should().Be(DeviceRenameStatus.NotFound);
    }

    [TestCase("   ", DeviceValidationCode.Required)]
    [TestCase("Bad\u0000Name", DeviceValidationCode.InvalidFormat)]
    public async Task RenameAsync_InvalidName_ReturnsTheError(string name, DeviceValidationCode code)
    {
        await _context.Repository.RegisterAsync(_userId, "phone", "Phone", DeviceType.Phone, DeviceTestContext.Start);

        DeviceRenameResult result = await _context.Service.RenameAsync(_userId, "phone", name);

        result.Status.Should().Be(DeviceRenameStatus.Invalid);
        result.Error!.Code.Should().Be(code);
        (await _context.Repository.FindAsync(_userId, "phone"))!.CustomName.Should().BeNull();
    }

    [Test]
    public async Task DeleteAsync_OpenDevice_ReturnsOnlineAndKeepsIt()
    {
        await _context.RegisterAsync(_userId, "laptop");

        DeviceDeleteStatus status = await _context.Service.DeleteAsync(_userId, "laptop");

        status.Should().Be(DeviceDeleteStatus.Online);
        (await _context.Repository.FindAsync(_userId, "laptop")).Should().NotBeNull();
    }

    [Test]
    public async Task DeleteAsync_ClosedDevice_ForgetsItAndTellsTheOpenDevices()
    {
        DeviceConnection laptop = await _context.RegisterAsync(_userId, "laptop");
        await _context.Repository.RegisterAsync(_userId, "phone", "Phone", DeviceType.Phone, DeviceTestContext.Start);
        _context.Notifier.Clear();

        DeviceDeleteStatus status = await _context.Service.DeleteAsync(_userId, "phone");

        status.Should().Be(DeviceDeleteStatus.Deleted);
        (await _context.Repository.FindAsync(_userId, "phone")).Should().BeNull();
        _context.Notifier.Of<IReadOnlyList<DeviceView>>(laptop.ConnectionId).Single().Select(device => device.Id)
            .Should().Equal("laptop");
    }

    [Test]
    public async Task DeleteAsync_UnknownDevice_ReturnsNotFound()
    {
        DeviceDeleteStatus status = await _context.Service.DeleteAsync(_userId, "missing");

        status.Should().Be(DeviceDeleteStatus.NotFound);
    }
}
