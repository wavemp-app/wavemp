using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using WaveMP.Core.Devices.Coordination;
using WaveMP.Core.Devices.Playbacks;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// The devices of a profile and the tickets of the real-time connection through the API. The devices register
/// directly with the coordinator, as the real-time connection does.
/// </summary>
[TestFixture]
[Category("Integration")]
public class MyDevicesApiTests
{
    private const string Path = "/api/v1/me/devices";
    private const string TicketPath = "/api/v1/me/device-hub-tickets";

    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private IDeviceCoordinator _coordinator = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_devices",
            freshDatabase: true);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        _coordinator = _factory.Services.GetRequiredService<IDeviceCoordinator>();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task Devices_WithoutToken_ReturnUnauthorized()
    {
        HttpResponseMessage list = await _anonymous.GetAsync(Path);
        HttpResponseMessage update = await _anonymous.PatchAsJsonAsync($"{Path}/phone", new { name = "Phone" });
        HttpResponseMessage delete = await _anonymous.DeleteAsync($"{Path}/phone");
        HttpResponseMessage ticket = await _anonymous.PostAsync(TicketPath, null);

        await list.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await update.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await delete.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await ticket.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task ListMyDevices_OpenDevices_ReturnsThemWithTheirLiveStateFirst()
    {
        Profile profile = await CreateProfileAsync();
        Profile other = await CreateProfileAsync();
        DeviceConnection laptop = await RegisterAsync(profile, "laptop", "Firefox · Windows", DeviceType.Desktop, true);
        DeviceConnection phone = await RegisterAsync(profile, "phone", "Safari · iPhone", DeviceType.Phone, false);
        await RegisterAsync(other, "laptop", "Edge · Windows", DeviceType.Desktop, true);
        await _coordinator.PublishAsync(laptop, new PublishPlaybackCommand(true, Playback()));
        await _coordinator.DisconnectAsync(phone);
        await RegisterAsync(profile, "tablet", "Chrome · Android", DeviceType.Tablet, false);

        HttpResponseMessage response = await profile.Client.GetAsync(Path);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        PageDto<DeviceDto> page = (await response.Content.ReadFromJsonAsync<PageDto<DeviceDto>>(
            HttpClientExtensions.JsonOptions))!;
        page.Total.Should().Be(3);
        page.Items.Select(device => device.Id).Should().Equal("tablet", "laptop", "phone");
        page.Items[1].Should().BeEquivalentTo(new
        {
            Id = "laptop",
            Name = "Firefox · Windows",
            ReportedName = "Firefox · Windows",
            CustomName = (string?)null,
            Type = DeviceTypeDto.Desktop,
            Online = true,
            RemoteControl = true,
            Active = true,
        });
        page.Items[2].Should().BeEquivalentTo(new
        {
            Type = DeviceTypeDto.Phone,
            Online = false,
            RemoteControl = false,
            Active = false,
        });
        (await other.Client.GetJsonAsync<PageDto<DeviceDto>>(Path)).Items.Should().ContainSingle()
            .Which.Name.Should().Be("Edge · Windows");
    }

    [Test]
    public async Task ListMyDevices_WithOffsetAndLimit_ReturnsThePage()
    {
        Profile profile = await CreateProfileAsync();
        foreach (string id in new[] { "a", "b", "c" })
        {
            await DisconnectAsync(await RegisterAsync(profile, id, $"Device {id}", DeviceType.Desktop, false));
        }

        PageDto<DeviceDto> page = await profile.Client.GetJsonAsync<PageDto<DeviceDto>>($"{Path}?offset=1&limit=1");

        page.Should().BeEquivalentTo(new { Offset = 1, Limit = 1, Total = 3 });
        page.Items.Should().ContainSingle();
    }

    [Test]
    public async Task ListMyDevices_InvalidLimit_ReturnsOutOfRange()
    {
        Profile profile = await CreateProfileAsync();

        HttpResponseMessage response = await profile.Client.GetAsync($"{Path}?limit=0");

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Field.Should().Be("limit");
    }

    [Test]
    public async Task UpdateMyDevice_NewName_RenamesTheDevice()
    {
        Profile profile = await CreateProfileAsync();
        await RegisterAsync(profile, "phone", "Safari · iPhone", DeviceType.Phone, true);

        HttpResponseMessage response = await profile.Client.PatchAsJsonAsync($"{Path}/phone", new { name = "  Kitchen  " });

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        DeviceDto device = (await response.Content.ReadFromJsonAsync<DeviceDto>(HttpClientExtensions.JsonOptions))!;
        device.Should().BeEquivalentTo(new
        {
            Id = "phone",
            Name = "Kitchen",
            CustomName = "Kitchen",
            ReportedName = "Safari · iPhone",
            Online = true,
            RemoteControl = true,
        });
        (await profile.Client.GetJsonAsync<PageDto<DeviceDto>>(Path)).Items.Single().Name.Should().Be("Kitchen");
    }

    [Test]
    public async Task UpdateMyDevice_ReportedName_RemovesTheGivenName()
    {
        Profile profile = await CreateProfileAsync();
        await RegisterAsync(profile, "phone", "Safari · iPhone", DeviceType.Phone, false);
        await profile.Client.PatchAsJsonAsync($"{Path}/phone", new { name = "Kitchen" });

        DeviceDto device = await PatchAsync(profile, "phone", new { name = "Safari · iPhone" });

        device.CustomName.Should().BeNull();
        device.Name.Should().Be("Safari · iPhone");
    }

    [Test]
    public async Task UpdateMyDevice_WithoutName_ReturnsTheDeviceUnchanged()
    {
        Profile profile = await CreateProfileAsync();
        await RegisterAsync(profile, "phone", "Safari · iPhone", DeviceType.Phone, false);

        DeviceDto device = await PatchAsync(profile, "phone", new { });

        device.Name.Should().Be("Safari · iPhone");
    }

    [TestCase("   ", "required")]
    [TestCase("Bad\u0007Name", "invalid_format")]
    public async Task UpdateMyDevice_InvalidName_ReturnsTheCode(string name, string code)
    {
        Profile profile = await CreateProfileAsync();
        await RegisterAsync(profile, "phone", "Phone", DeviceType.Phone, false);

        HttpResponseMessage response = await profile.Client.PatchAsJsonAsync($"{Path}/phone", new { name });

        string content = await response.Content.ReadAsStringAsync();
        await response.ShouldBeValidationProblemAsync();
        content.Should().Contain("\"field\":\"name\"").And.Contain($"\"code\":\"{code}\"");
    }

    [Test]
    public async Task UpdateMyDevice_TooLongName_ReturnsMaxLength()
    {
        Profile profile = await CreateProfileAsync();
        await RegisterAsync(profile, "phone", "Phone", DeviceType.Phone, false);

        HttpResponseMessage response = await profile.Client.PatchAsJsonAsync(
            $"{Path}/phone",
            new { name = new string('x', 101) });

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Field = "name", Code = ValidationErrorCodeDto.MaxLength, Max = 100 });
    }

    [Test]
    public async Task UpdateMyDevice_DeviceOfAnotherProfile_ReturnsNotFoundAndKeepsIt()
    {
        Profile profile = await CreateProfileAsync();
        Profile other = await CreateProfileAsync();
        await RegisterAsync(other, "phone", "Phone", DeviceType.Phone, false);

        HttpResponseMessage renamed = await profile.Client.PatchAsJsonAsync($"{Path}/phone", new { name = "Mine" });
        HttpResponseMessage read = await profile.Client.PatchAsJsonAsync($"{Path}/phone", new { });

        await renamed.ShouldBeProblemAsync(HttpStatusCode.NotFound, "device.not_found");
        await read.ShouldBeProblemAsync(HttpStatusCode.NotFound, "device.not_found");
        (await other.Client.GetJsonAsync<PageDto<DeviceDto>>(Path)).Items.Single().Name.Should().Be("Phone");
    }

    [Test]
    public async Task DeviceRoutes_InvalidDeviceId_ReturnNotFound()
    {
        Profile profile = await CreateProfileAsync();

        HttpResponseMessage update = await profile.Client.PatchAsJsonAsync($"{Path}/bad%20id", new { name = "x" });
        HttpResponseMessage delete = await profile.Client.DeleteAsync($"{Path}/{new string('a', 65)}");

        update.StatusCode.Should().Be(HttpStatusCode.NotFound);
        delete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteMyDevice_OpenDevice_ReturnsConflictAndKeepsIt()
    {
        Profile profile = await CreateProfileAsync();
        await RegisterAsync(profile, "phone", "Phone", DeviceType.Phone, false);

        HttpResponseMessage response = await profile.Client.DeleteAsync($"{Path}/phone");

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "device.online");
        (await profile.Client.GetJsonAsync<PageDto<DeviceDto>>(Path)).Items.Should().ContainSingle();
    }

    [Test]
    public async Task DeleteMyDevice_ClosedDevice_ForgetsIt()
    {
        Profile profile = await CreateProfileAsync();
        await DisconnectAsync(await RegisterAsync(profile, "phone", "Phone", DeviceType.Phone, false));

        HttpResponseMessage response = await profile.Client.DeleteAsync($"{Path}/phone");
        HttpResponseMessage again = await profile.Client.DeleteAsync($"{Path}/phone");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await again.ShouldBeProblemAsync(HttpStatusCode.NotFound, "device.not_found");
        (await profile.Client.GetJsonAsync<PageDto<DeviceDto>>(Path)).Items.Should().BeEmpty();
    }

    [Test]
    public async Task DeleteMyDevice_DeviceOfAnotherProfile_ReturnsNotFoundAndKeepsIt()
    {
        Profile profile = await CreateProfileAsync();
        Profile other = await CreateProfileAsync();
        await DisconnectAsync(await RegisterAsync(other, "phone", "Phone", DeviceType.Phone, false));

        HttpResponseMessage response = await profile.Client.DeleteAsync($"{Path}/phone");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "device.not_found");
        (await other.Client.GetJsonAsync<PageDto<DeviceDto>>(Path)).Items.Should().ContainSingle();
    }

    [Test]
    public async Task CreateDeviceHubTicket_Always_ReturnsATicketOfTheSessionForAMinute()
    {
        Profile profile = await CreateProfileAsync();
        DateTimeOffset before = DateTimeOffset.UtcNow;

        HttpResponseMessage response = await profile.Client.PostAsync(TicketPath, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Headers.CacheControl!.NoStore.Should().BeTrue();
        DeviceHubTicketDto ticket = (await response.Content.ReadFromJsonAsync<DeviceHubTicketDto>(
            HttpClientExtensions.JsonOptions))!;
        ticket.ExpiresAt.Should().BeCloseTo(before.AddSeconds(60), TimeSpan.FromSeconds(10));
        JsonWebToken jwt = new(ticket.Ticket);
        jwt.Audiences.Should().Equal("wavemp-device-hub");
        jwt.Subject.Should().Be(profile.Id.ToString());
        jwt.GetClaim("sid").Value.Should().Be(profile.SessionId.ToString());
    }

    [Test]
    public async Task DeviceHubTicket_AsTokenOfTheApi_IsRejected()
    {
        Profile profile = await CreateProfileAsync();
        DeviceHubTicketDto ticket = (await (await profile.Client.PostAsync(TicketPath, null)).Content
            .ReadFromJsonAsync<DeviceHubTicketDto>(HttpClientExtensions.JsonOptions))!;
        using HttpClient withTicket = _factory.CreateClient();
        withTicket.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ticket.Ticket);

        HttpResponseMessage devices = await withTicket.GetAsync(Path);
        HttpResponseMessage session = await withTicket.GetAsync("/api/v1/sessions/current");
        HttpResponseMessage another = await withTicket.PostAsync(TicketPath, null);

        await devices.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await session.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await another.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task DeletingTheProfile_Always_ForgetsItsDevices()
    {
        Profile profile = await CreateProfileAsync();
        await DisconnectAsync(await RegisterAsync(profile, "phone", "Phone", DeviceType.Phone, false));

        (await _admin.DeleteAsync($"/api/v1/users/{profile.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Profile again = await CreateProfileAsync();
        (await again.Client.GetJsonAsync<PageDto<DeviceDto>>(Path)).Items.Should().BeEmpty();
    }

    private async Task<DeviceConnection> RegisterAsync(
        Profile profile,
        string deviceId,
        string name,
        DeviceType type,
        bool remoteControl)
    {
        DeviceConnection connection = new(profile.Id, profile.SessionId, $"{deviceId}-{HttpClientExtensions.Unique()}");
        RegisterResult result = await _coordinator.RegisterAsync(
            connection,
            new RegisterDeviceCommand(deviceId, name, type, remoteControl));
        result.IsRegistered.Should().BeTrue();
        return connection;
    }

    private Task DisconnectAsync(DeviceConnection connection)
    {
        return _coordinator.DisconnectAsync(connection);
    }

    private static async Task<DeviceDto> PatchAsync(Profile profile, string deviceId, object request)
    {
        HttpResponseMessage response = await profile.Client.PatchAsJsonAsync($"{Path}/{deviceId}", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<DeviceDto>(HttpClientExtensions.JsonOptions))!;
    }

    private static LivePlaybackInput Playback()
    {
        return new LivePlaybackInput(
            LivePlaybackStatus.Paused,
            new LiveQueueInput([], false, [], null),
            -1,
            0,
            0,
            RepeatMode.Off,
            1,
            false,
            false,
            false);
    }

    private async Task<Profile> CreateProfileAsync()
    {
        UserDto user = await _admin.CreateUserAsync(new { name = $"Devices {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        return new Profile(user.Id, session.Session.Id, _factory.CreateAuthorizedClient(session.Token));
    }

    private sealed record Profile(Guid Id, Guid SessionId, HttpClient Client);
}
