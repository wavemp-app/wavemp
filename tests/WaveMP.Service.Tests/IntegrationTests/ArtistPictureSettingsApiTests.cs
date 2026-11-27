using System.Net;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// The settings of the artist pictures: off by default, admins only, and the API key of fanart.tv is write-only.
/// </summary>
[TestFixture]
[Category("Integration")]
public class ArtistPictureSettingsApiTests
{
    private const string Path = "/api/v1/settings/artist-pictures";
    private const string ApiKey = "0123456789abcdefFEDCBA9876543210";

    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _user = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new WaveMpWebApplicationFactory(
            "wavemp_tests_service_artist_picture_settings",
            freshDatabase: true);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        UserDto user = await _admin.CreateUserAsync(new { name = $"Listener {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        _user = _factory.CreateAuthorizedClient(session.Token);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _user?.Dispose();
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _admin.SendJsonAsync("PATCH", Path, """{ "onlineLookupEnabled": false, "fanartTvApiKey": "" }""");
    }

    [Test]
    public async Task GetArtistPictureSettings_OfANewInstallation_AreOffWithoutApiKey()
    {
        ArtistPictureSettingsDto settings = await _admin.GetJsonAsync<ArtistPictureSettingsDto>(Path);

        settings.Should().BeEquivalentTo(new ArtistPictureSettingsDto
        {
            OnlineLookupEnabled = false,
            FanartTvApiKeySet = false,
        });
    }

    [Test]
    public async Task UpdateArtistPictureSettings_WithApiKey_StoresItButNeverReturnsIt()
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PATCH",
            Path,
            $$"""{ "onlineLookupEnabled": true, "fanartTvApiKey": " {{ApiKey}} " }""");
        string updated = await response.Content.ReadAsStringAsync();
        HttpResponseMessage read = await _admin.GetAsync(Path);
        string loaded = await read.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, updated);
        updated.Should().NotContain(ApiKey).And.NotContain("apiKey\"");
        loaded.Should().NotContain(ApiKey);
        (await _admin.GetJsonAsync<ArtistPictureSettingsDto>(Path)).Should().BeEquivalentTo(
            new ArtistPictureSettingsDto { OnlineLookupEnabled = true, FanartTvApiKeySet = true });
    }

    [Test]
    public async Task UpdateArtistPictureSettings_WithoutApiKey_KeepsItAndAnEmptyOneRemovesIt()
    {
        await _admin.SendJsonAsync("PATCH", Path, $$"""{ "fanartTvApiKey": "{{ApiKey}}" }""");

        HttpResponseMessage kept = await _admin.SendJsonAsync("PATCH", Path, """{ "onlineLookupEnabled": true }""");
        ArtistPictureSettingsDto afterKept = await _admin.GetJsonAsync<ArtistPictureSettingsDto>(Path);
        HttpResponseMessage removed = await _admin.SendJsonAsync("PATCH", Path, """{ "fanartTvApiKey": "" }""");
        ArtistPictureSettingsDto afterRemoved = await _admin.GetJsonAsync<ArtistPictureSettingsDto>(Path);

        kept.StatusCode.Should().Be(HttpStatusCode.OK);
        afterKept.FanartTvApiKeySet.Should().BeTrue();
        removed.StatusCode.Should().Be(HttpStatusCode.OK);
        afterRemoved.Should().BeEquivalentTo(
            new ArtistPictureSettingsDto { OnlineLookupEnabled = true, FanartTvApiKeySet = false });
    }

    [TestCase("not a key")]
    [TestCase("key-with-dash")]
    [TestCase("abcdefghijabcdefghijabcdefghijabcdefghijabcdefghijabcdefghij12345")]
    public async Task UpdateArtistPictureSettings_InvalidApiKey_ReturnsInvalidFormatWithoutTheValue(string key)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PATCH",
            Path,
            $$"""{ "onlineLookupEnabled": true, "fanartTvApiKey": "{{key}}" }""");
        string body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain(key);
        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Select(error => (error.Field, error.Code))
            .Should().Equal(("fanartTvApiKey", ValidationErrorCodeDto.InvalidFormat));
        (await _admin.GetJsonAsync<ArtistPictureSettingsDto>(Path)).Should().BeEquivalentTo(
            new ArtistPictureSettingsDto { OnlineLookupEnabled = false, FanartTvApiKeySet = false });
    }

    [TestCase("GET")]
    [TestCase("PATCH")]
    public async Task ArtistPictureSettings_AsListener_ReturnsForbidden(string method)
    {
        HttpResponseMessage response = await _user.SendJsonAsync(
            method,
            Path,
            method == "PATCH" ? """{ "onlineLookupEnabled": true }""" : null);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Test]
    public async Task ArtistPictureSettings_WithoutSession_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _anonymous.GetAsync(Path);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
