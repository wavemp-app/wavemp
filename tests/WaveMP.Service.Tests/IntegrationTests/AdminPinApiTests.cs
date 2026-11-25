using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// The PIN of the built-in admin profile from <c>ADMIN_PIN</c>.
/// </summary>
[TestFixture]
[Category("Integration")]
public class AdminPinApiTests
{
    private const string AdminPin = "246810";
    private const string DatabaseName = WaveMpWebApplicationFactory.TestDatabaseName + "_admin_pin";

    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private ProfileDto _adminProfile = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = CreateFactory(AdminPin, freshDatabase: true);
        _anonymous = _factory.CreateClient();
        _adminProfile = await _anonymous.GetBuiltInAdminAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
    }

    [Test]
    public void AdminProfile_WithAdminPin_IsProtected()
    {
        _adminProfile.HasPin.Should().BeTrue();
    }

    [Test]
    public async Task CreateSession_AdminWithoutPin_ReturnsPinRequired()
    {
        HttpResponseMessage response = await _anonymous.PostAsJsonAsync(
            "/api/v1/sessions",
            new { profileId = _adminProfile.Id });

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "profile.pin_required");
    }

    [Test]
    public async Task AdminPin_WhileSet_CannotBeChangedRemovedOrReset()
    {
        using HttpClient admin = await CreateAdminClientAsync();

        PinStatusDto status = await admin.GetJsonAsync<PinStatusDto>("/api/v1/me/pin");
        HttpResponseMessage changed = await admin.PutAsJsonAsync("/api/v1/me/pin", new { pin = "1234" });
        HttpResponseMessage removed = await admin.DeleteAsync("/api/v1/me/pin");
        HttpResponseMessage reset = await admin.DeleteAsync($"/api/v1/users/{_adminProfile.Id}/pin");

        status.IsSet.Should().BeTrue();
        status.IsManagedByEnvironment.Should().BeTrue();
        await changed.ShouldBeProblemAsync(HttpStatusCode.Conflict, "pin.managed_by_environment");
        await removed.ShouldBeProblemAsync(HttpStatusCode.Conflict, "pin.managed_by_environment");
        await reset.ShouldBeProblemAsync(HttpStatusCode.Conflict, "pin.managed_by_environment");
    }

    [Test]
    public async Task OtherProfiles_WithAdminPin_KeepTheirOwnPins()
    {
        using HttpClient admin = await CreateAdminClientAsync();
        UserDto user = await admin.CreateUserAsync(new { name = $"Other {HttpClientExtensions.Unique()}" });
        using HttpClient client = _factory.CreateAuthorizedClient((await _anonymous.CreateSessionAsync(user.Id)).Token);

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/v1/me/pin", new { pin = "1234" });
        PinStatusDto status = await client.GetJsonAsync<PinStatusDto>("/api/v1/me/pin");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        status.IsManagedByEnvironment.Should().BeFalse();
    }

    [Test]
    public async Task Restart_WithAnotherAdminPin_OverridesThePinAndEndsTheAdminSessions()
    {
        const string RestartDatabaseName = DatabaseName + "_restart";
        string token;
        await using (WaveMpWebApplicationFactory first = CreateFactory(null, freshDatabase: true, RestartDatabaseName))
        {
            using HttpClient anonymous = first.CreateClient();
            ProfileDto admin = await anonymous.GetBuiltInAdminAsync();
            token = (await anonymous.CreateSessionAsync(admin.Id)).Token;
            admin.HasPin.Should().BeFalse();
        }

        await using WaveMpWebApplicationFactory second = CreateFactory(
            "1357",
            freshDatabase: false,
            RestartDatabaseName);
        try
        {
            using HttpClient anonymous = second.CreateClient();
            using HttpClient old = second.CreateAuthorizedClient(token);
            ProfileDto admin = await anonymous.GetBuiltInAdminAsync();

            admin.HasPin.Should().BeTrue();
            await (await old.GetAsync("/api/v1/sessions/current"))
                .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
            (await anonymous.PostAsJsonAsync("/api/v1/sessions", new { profileId = admin.Id, pin = "1357" }))
                .StatusCode.Should().Be(HttpStatusCode.Created);
        }
        finally
        {
            await second.DeleteDatabaseAsync();
        }
    }

    [TestCase("123")]
    [TestCase("12ab")]
    [TestCase("123456789")]
    public async Task Start_WithInvalidAdminPin_Fails(string pin)
    {
        await using WaveMpWebApplicationFactory factory = CreateFactory(pin, freshDatabase: false, DatabaseName);

        Action start = () => factory.CreateClient();

        start.Should().Throw<InvalidOperationException>().WithMessage("*PIN configuration*");
    }

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        HttpResponseMessage response = await _anonymous.PostAsJsonAsync(
            "/api/v1/sessions",
            new { profileId = _adminProfile.Id, pin = AdminPin });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        CreatedSessionDto session = (await response.Content.ReadFromJsonAsync<CreatedSessionDto>(
            HttpClientExtensions.JsonOptions))!;
        return _factory.CreateAuthorizedClient(session.Token);
    }

    private static WaveMpWebApplicationFactory CreateFactory(
        string? adminPin,
        bool freshDatabase,
        string databaseName = DatabaseName)
    {
        return new WaveMpWebApplicationFactory(
            databaseName,
            freshDatabase,
            settings: new Dictionary<string, string?> { ["ADMIN_PIN"] = adminPin ?? string.Empty });
    }
}
