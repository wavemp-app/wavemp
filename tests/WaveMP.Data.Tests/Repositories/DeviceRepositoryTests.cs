using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using WaveMP.Data.Entities;
using WaveMP.Data.Repositories;

namespace WaveMP.Data.Tests.Repositories;

[TestFixture]
[Category("Integration")]
public class DeviceRepositoryTests : LibraryDatabaseTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 25, 8, 0, 0, TimeSpan.Zero);

    protected override string DatabaseName => "wavemp_tests_data_devices";

    [Test]
    public async Task RegisterAsync_NewDevice_AddsItWithAllValues()
    {
        User user = await AddUserAsync();

        Device device = await CreateRepository().RegisterAsync(
            user.Id,
            "phone",
            "Safari · iPhone",
            DeviceType.Phone,
            _start);

        device.UserId.Should().Be(user.Id);
        device.DeviceId.Should().Be("phone");
        device.ReportedName.Should().Be("Safari · iPhone");
        device.CustomName.Should().BeNull();
        device.Type.Should().Be(DeviceType.Phone);
        device.CreatedAt.Should().Be(_start);
        device.LastActiveAt.Should().Be(_start);
        (await CreateRepository().FindAsync(user.Id, "phone")).Should().BeEquivalentTo(device);
    }

    [Test]
    public async Task RegisterAsync_KnownDevice_UpdatesReportedNameTypeAndActivityAndKeepsItsCustomName()
    {
        User user = await AddUserAsync();
        await CreateRepository().RegisterAsync(user.Id, "tablet", "Chrome · Android", DeviceType.Phone, _start);
        await CreateRepository().RenameAsync(user.Id, "tablet", "Kitchen");

        Device device = await CreateRepository().RegisterAsync(
            user.Id,
            "tablet",
            "Chrome · ChromeOS",
            DeviceType.Tablet,
            _start.AddHours(1));

        device.ReportedName.Should().Be("Chrome · ChromeOS");
        device.CustomName.Should().Be("Kitchen");
        device.Type.Should().Be(DeviceType.Tablet);
        device.CreatedAt.Should().Be(_start);
        device.LastActiveAt.Should().Be(_start.AddHours(1));
    }

    [Test]
    public async Task RegisterAsync_SameDeviceIdOfTwoProfiles_KeepsTwoDevices()
    {
        User user = await AddUserAsync();
        User other = await AddUserAsync();

        await CreateRepository().RegisterAsync(user.Id, "shared", "Firefox · Linux", DeviceType.Desktop, _start);
        await CreateRepository().RegisterAsync(other.Id, "shared", "Edge · Windows", DeviceType.Desktop, _start);

        (await CreateRepository().FindAsync(user.Id, "shared"))!.ReportedName.Should().Be("Firefox · Linux");
        (await CreateRepository().FindAsync(other.Id, "shared"))!.ReportedName.Should().Be("Edge · Windows");
    }

    [Test]
    public async Task RegisterAsync_UnknownUser_ThrowsUserNotFound()
    {
        var userId = Guid.NewGuid();

        Func<Task> register = () => CreateRepository().RegisterAsync(
            userId,
            "phone",
            "Phone",
            DeviceType.Phone,
            _start);

        (await register.Should().ThrowAsync<UserNotFoundException>()).Which.UserId.Should().Be(userId);
    }

    [Test]
    public async Task ListAsync_Always_ReturnsTheDevicesOfTheProfileMostRecentlyActiveFirst()
    {
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        await CreateRepository().RegisterAsync(user.Id, "old", "Old", DeviceType.Desktop, _start);
        await CreateRepository().RegisterAsync(user.Id, "new", "New", DeviceType.Phone, _start.AddMinutes(5));
        await CreateRepository().RegisterAsync(user.Id, "middle", "Middle", DeviceType.Tablet, _start.AddMinutes(2));
        await CreateRepository().RegisterAsync(other.Id, "foreign", "Foreign", DeviceType.Tv, _start.AddMinutes(9));

        IReadOnlyList<Device> devices = await CreateRepository().ListAsync(user.Id);

        devices.Select(device => device.DeviceId).Should().Equal("new", "middle", "old");
    }

    [Test]
    public async Task RenameAsync_KnownDevice_SetsAndClearsTheCustomName()
    {
        User user = await AddUserAsync();
        await CreateRepository().RegisterAsync(user.Id, "phone", "Safari · iPhone", DeviceType.Phone, _start);

        Device? renamed = await CreateRepository().RenameAsync(user.Id, "phone", "Lars's iPhone");
        Device? cleared = await CreateRepository().RenameAsync(user.Id, "phone", null);

        renamed!.CustomName.Should().Be("Lars's iPhone");
        cleared!.CustomName.Should().BeNull();
        cleared.ReportedName.Should().Be("Safari · iPhone");
    }

    [Test]
    public async Task RenameAsync_DeviceOfAnotherProfile_ReturnsNullAndKeepsIt()
    {
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        await CreateRepository().RegisterAsync(other.Id, "phone", "Phone", DeviceType.Phone, _start);

        Device? renamed = await CreateRepository().RenameAsync(user.Id, "phone", "Mine");

        renamed.Should().BeNull();
        (await CreateRepository().FindAsync(other.Id, "phone"))!.CustomName.Should().BeNull();
    }

    [Test]
    public async Task DeleteAsync_KnownDevice_RemovesOnlyThisDevice()
    {
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        await CreateRepository().RegisterAsync(user.Id, "phone", "Phone", DeviceType.Phone, _start);
        await CreateRepository().RegisterAsync(user.Id, "laptop", "Laptop", DeviceType.Desktop, _start);
        await CreateRepository().RegisterAsync(other.Id, "phone", "Phone", DeviceType.Phone, _start);

        bool deleted = await CreateRepository().DeleteAsync(user.Id, "phone");

        deleted.Should().BeTrue();
        (await CreateRepository().ListAsync(user.Id)).Select(device => device.DeviceId).Should().Equal("laptop");
        (await CreateRepository().FindAsync(other.Id, "phone")).Should().NotBeNull();
    }

    [Test]
    public async Task DeleteAsync_UnknownDevice_ReturnsFalse()
    {
        User user = await AddUserAsync();

        bool deleted = await CreateRepository().DeleteAsync(user.Id, "missing");

        deleted.Should().BeFalse();
    }

    [Test]
    public async Task TouchAsync_LaterTime_MovesTheLastActivity()
    {
        User user = await AddUserAsync();
        await CreateRepository().RegisterAsync(user.Id, "phone", "Phone", DeviceType.Phone, _start);

        await CreateRepository().TouchAsync(user.Id, "phone", _start.AddMinutes(3));

        (await CreateRepository().FindAsync(user.Id, "phone"))!.LastActiveAt.Should().Be(_start.AddMinutes(3));
    }

    [Test]
    public async Task TouchAsync_EarlierTime_KeepsTheLastActivity()
    {
        User user = await AddUserAsync();
        await CreateRepository().RegisterAsync(user.Id, "phone", "Phone", DeviceType.Phone, _start);

        await CreateRepository().TouchAsync(user.Id, "phone", _start.AddMinutes(-3));

        (await CreateRepository().FindAsync(user.Id, "phone"))!.LastActiveAt.Should().Be(_start);
    }

    [Test]
    public async Task EvictAsync_AboveTheLimit_RemovesTheLeastRecentlyActiveDevicesExceptTheKeptOnes()
    {
        User user = await AddUserAsync();
        User other = await AddUserAsync();
        for (int index = 0; index < 5; index++)
        {
            await CreateRepository().RegisterAsync(
                user.Id,
                $"device-{index}",
                $"Device {index}",
                DeviceType.Desktop,
                _start.AddMinutes(index));
        }

        await CreateRepository().RegisterAsync(other.Id, "device-0", "Other", DeviceType.Desktop, _start);

        int removed = await CreateRepository().EvictAsync(user.Id, 3, ["device-0"]);

        removed.Should().Be(2);
        (await CreateRepository().ListAsync(user.Id)).Select(device => device.DeviceId)
            .Should().Equal("device-4", "device-3", "device-0");
        (await CreateRepository().FindAsync(other.Id, "device-0")).Should().NotBeNull();
    }

    [Test]
    public async Task EvictAsync_WithinTheLimit_RemovesNothing()
    {
        User user = await AddUserAsync();
        await CreateRepository().RegisterAsync(user.Id, "phone", "Phone", DeviceType.Phone, _start);
        await CreateRepository().RegisterAsync(user.Id, "laptop", "Laptop", DeviceType.Desktop, _start);

        int removed = await CreateRepository().EvictAsync(user.Id, 2, []);

        removed.Should().Be(0);
        (await CreateRepository().ListAsync(user.Id)).Should().HaveCount(2);
    }

    [Test]
    public async Task DeletingTheUser_Always_DeletesItsDevices()
    {
        User user = await AddUserAsync();
        await CreateRepository().RegisterAsync(user.Id, "phone", "Phone", DeviceType.Phone, _start);

        await using (WaveMpDbContext dbContext = CreateDbContext())
        {
            await dbContext.Users.Where(candidate => candidate.Id == user.Id).ExecuteDeleteAsync();
        }

        (await CreateRepository().ListAsync(user.Id)).Should().BeEmpty();
    }

    private IDeviceRepository CreateRepository()
    {
        return new DeviceRepository(CreateDbContext());
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
}
