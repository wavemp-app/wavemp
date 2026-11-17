using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WaveMP.Data;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// PINs of the profiles: selecting a protected profile, the lockout after wrong PINs, the own PIN and the reset by an
/// admin.
/// </summary>
[TestFixture]
[Category("Integration")]
public class PinsApiTests
{
    private const int MaxAttempts = 3;

    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_pins",
            freshDatabase: true,
            settings: new Dictionary<string, string?>
            {
                ["Pins:MaxFailedAttempts"] = MaxAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Pins:LockoutDuration"] = "00:05:00",
            });
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
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
    public async Task GetMyPin_WithoutPin_IsNotSet()
    {
        using HttpClient client = await CreateUserClientAsync();

        PinStatusDto status = await client.GetJsonAsync<PinStatusDto>("/api/v1/me/pin");

        status.IsSet.Should().BeFalse();
        status.IsManagedByEnvironment.Should().BeFalse();
    }

    [Test]
    public async Task SetMyPin_ValidPin_ProtectsTheProfile()
    {
        (UserDto user, HttpClient client) = await CreateUserWithClientAsync();
        using (client)
        {
            HttpResponseMessage response = await client.PutAsJsonAsync("/api/v1/me/pin", new { pin = "4826" });

            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            PinStatusDto status = (await response.Content.ReadFromJsonAsync<PinStatusDto>(
                HttpClientExtensions.JsonOptions))!;
            status.IsSet.Should().BeTrue();
            (await client.GetJsonAsync<PinStatusDto>("/api/v1/me/pin")).IsSet.Should().BeTrue();
            (await GetProfileAsync(user.Id)).HasPin.Should().BeTrue();
            (await _admin.GetJsonAsync<UserDto>($"/api/v1/users/{user.Id}")).HasPin.Should().BeTrue();
        }
    }

    [Test]
    public async Task SetMyPin_Always_StoresOnlyAHash()
    {
        (UserDto user, HttpClient client) = await CreateUserWithClientAsync();
        using (client)
        {
            await SetPinAsync(client, "73915");

            await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
            WaveMpDbContext dbContext = scope.ServiceProvider.GetRequiredService<WaveMpDbContext>();
            string? hash = await dbContext.Users
                .Where(candidate => candidate.Id == user.Id)
                .Select(candidate => candidate.PinHash)
                .SingleAsync();
            hash.Should().NotBeNullOrEmpty();
            hash.Should().NotContain("73915");
        }
    }

    [Test]
    public async Task SetMyPin_Always_EndsTheOtherSessionsOfTheProfile()
    {
        UserDto user = await _admin.CreateUserAsync(new { name = $"Devices {HttpClientExtensions.Unique()}" });
        using HttpClient current = _factory.CreateAuthorizedClient(
            (await _anonymous.CreateSessionAsync(user.Id)).Token);
        using HttpClient other = _factory.CreateAuthorizedClient(
            (await _anonymous.CreateSessionAsync(user.Id)).Token);

        await SetPinAsync(current, "1357");

        (await current.GetAsync("/api/v1/sessions/current")).StatusCode.Should().Be(HttpStatusCode.OK);
        await (await other.GetAsync("/api/v1/sessions/current"))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [TestCase("123")]
    [TestCase("123456789")]
    [TestCase("12a4")]
    [TestCase("")]
    public async Task SetMyPin_InvalidPin_ReturnsValidationProblem(string pin)
    {
        using HttpClient client = await CreateUserClientAsync();

        HttpResponseMessage response = await client.PutAsJsonAsync("/api/v1/me/pin", new { pin });

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Field.Should().Be("pin");
    }

    [Test]
    public async Task SetMyPin_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _anonymous.PutAsJsonAsync("/api/v1/me/pin", new { pin = "1234" });

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task CreateSession_ProtectedProfileWithoutPin_ReturnsPinRequired()
    {
        UserDto user = await CreateProtectedUserAsync("2580");

        HttpResponseMessage response = await PostSessionAsync(user.Id, null);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "profile.pin_required");
    }

    [Test]
    public async Task CreateSession_ProtectedProfileWithCorrectPin_CreatesTheSession()
    {
        UserDto user = await CreateProtectedUserAsync("2580");

        HttpResponseMessage response = await PostSessionAsync(user.Id, "2580");

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        CreatedSessionDto created = (await response.Content.ReadFromJsonAsync<CreatedSessionDto>(
            HttpClientExtensions.JsonOptions))!;
        created.Session.Profile.HasPin.Should().BeTrue();
    }

    [Test]
    public async Task CreateSession_ProtectedProfileWithWrongPin_ReturnsPinInvalid()
    {
        UserDto user = await CreateProtectedUserAsync("2580");

        HttpResponseMessage response = await PostSessionAsync(user.Id, "0852");

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "profile.pin_invalid");
    }

    [Test]
    public async Task CreateSession_MalformedPin_ReturnsValidationProblem()
    {
        UserDto user = await CreateProtectedUserAsync("2580");

        HttpResponseMessage response = await PostSessionAsync(user.Id, "25a0");

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new ValidationErrorDto { Field = "pin", Code = ValidationErrorCodeDto.InvalidFormat });
    }

    [Test]
    public async Task CreateSession_ProfileWithoutPin_IgnoresAGivenPin()
    {
        UserDto user = await _admin.CreateUserAsync(new { name = $"Open {HttpClientExtensions.Unique()}" });

        HttpResponseMessage response = await PostSessionAsync(user.Id, "1234");

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task CreateSession_TooManyWrongPins_LocksTheProfileOutTemporarily()
    {
        UserDto user = await CreateProtectedUserAsync("2580");
        for (int attempt = 1; attempt < MaxAttempts; attempt++)
        {
            await (await PostSessionAsync(user.Id, "1111"))
                .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "profile.pin_invalid");
        }

        HttpResponseMessage last = await PostSessionAsync(user.Id, "1111");
        HttpResponseMessage correct = await PostSessionAsync(user.Id, "2580");

        await last.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "profile.pin_locked");
        await correct.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "profile.pin_locked");
        TimeSpan retryAfter = correct.Headers.RetryAfter!.Delta!.Value;
        retryAfter.Should().BeGreaterThan(TimeSpan.FromMinutes(4)).And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
    }

    [Test]
    public async Task CreateSession_CorrectPin_ResetsTheCountOfWrongPins()
    {
        UserDto user = await CreateProtectedUserAsync("2580");
        for (int round = 0; round < 2; round++)
        {
            for (int attempt = 1; attempt < MaxAttempts; attempt++)
            {
                await (await PostSessionAsync(user.Id, "1111"))
                    .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "profile.pin_invalid");
            }

            (await PostSessionAsync(user.Id, "2580")).StatusCode.Should().Be(HttpStatusCode.Created);
        }
    }

    [Test]
    public async Task ResetUserPin_AsAdmin_RemovesThePinAndLiftsTheLockout()
    {
        UserDto user = await CreateProtectedUserAsync("2580");
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            await PostSessionAsync(user.Id, "1111");
        }

        HttpResponseMessage response = await _admin.DeleteAsync($"/api/v1/users/{user.Id}/pin");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetProfileAsync(user.Id)).HasPin.Should().BeFalse();
        (await PostSessionAsync(user.Id, null)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task ResetUserPin_UnknownUser_ReturnsNotFound()
    {
        HttpResponseMessage response = await _admin.DeleteAsync($"/api/v1/users/{Guid.NewGuid()}/pin");

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "user.not_found");
    }

    [Test]
    public async Task DeleteMyPin_ProtectedProfile_RemovesThePin()
    {
        (UserDto user, HttpClient client) = await CreateUserWithClientAsync();
        using (client)
        {
            await SetPinAsync(client, "2580");

            HttpResponseMessage response = await client.DeleteAsync("/api/v1/me/pin");

            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await GetProfileAsync(user.Id)).HasPin.Should().BeFalse();
            (await client.GetAsync("/api/v1/sessions/current")).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Test]
    public async Task AdminProfile_WithoutAdminPin_CanSetAndRemoveItsPin()
    {
        HttpResponseMessage set = await _admin.PutAsJsonAsync("/api/v1/me/pin", new { pin = "9753" });
        HttpResponseMessage removed = await _admin.DeleteAsync("/api/v1/me/pin");

        set.StatusCode.Should().Be(HttpStatusCode.OK);
        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _admin.GetJsonAsync<PinStatusDto>("/api/v1/me/pin")).IsSet.Should().BeFalse();
    }

    private async Task<UserDto> CreateProtectedUserAsync(string pin)
    {
        (UserDto user, HttpClient client) = await CreateUserWithClientAsync();
        using (client)
        {
            await SetPinAsync(client, pin);
        }

        return user;
    }

    private async Task<(UserDto User, HttpClient Client)> CreateUserWithClientAsync()
    {
        UserDto user = await _admin.CreateUserAsync(new { name = $"Pin {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        return (user, _factory.CreateAuthorizedClient(session.Token));
    }

    private async Task<HttpClient> CreateUserClientAsync()
    {
        (_, HttpClient client) = await CreateUserWithClientAsync();
        return client;
    }

    private static async Task SetPinAsync(HttpClient client, string pin)
    {
        HttpResponseMessage response = await client.PutAsJsonAsync("/api/v1/me/pin", new { pin });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private Task<HttpResponseMessage> PostSessionAsync(Guid profileId, string? pin)
    {
        return _anonymous.PostAsJsonAsync(
            "/api/v1/sessions",
            new { profileId, pin },
            HttpClientExtensions.JsonOptions);
    }

    private async Task<ProfileDto> GetProfileAsync(Guid id)
    {
        PageDto<ProfileDto> page = await _anonymous.GetJsonAsync<PageDto<ProfileDto>>("/api/v1/profiles?limit=100");
        return page.Items.Single(profile => profile.Id == id);
    }
}
