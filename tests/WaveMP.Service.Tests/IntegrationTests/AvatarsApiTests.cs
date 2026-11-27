using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

[TestFixture]
[Category("Integration")]
public class AvatarsApiTests
{
    private const string MyAvatarPath = "/api/v1/me/avatar";
    private const string PresetsPath = "/api/v1/avatar-presets";

    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private byte[] _picture = null!;
    private readonly List<HttpClient> _clients = [];

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        using (TemporaryDirectory upload = new())
        {
            await TestMediaGenerator.CreateImageAsync(upload.Combine("avatar.png"), 400, 250);
            _picture = await File.ReadAllBytesAsync(upload.Combine("avatar.png"));
        }

        _factory = new WaveMpWebApplicationFactory(
            "wavemp_tests_service_avatars",
            freshDatabase: true);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
    }

    [TearDown]
    public void TearDown()
    {
        foreach (HttpClient client in _clients)
        {
            client.Dispose();
        }

        _clients.Clear();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
    }

    private static IEnumerable<TestCaseData> SessionEndpoints()
    {
        yield return new TestCaseData("PUT", MyAvatarPath).SetArgDisplayNames("SetMyAvatar");
        yield return new TestCaseData("DELETE", MyAvatarPath).SetArgDisplayNames("DeleteMyAvatar");
        yield return new TestCaseData("GET", PresetsPath).SetArgDisplayNames("ListAvatarPresets");
        yield return new TestCaseData("GET", $"{PresetsPath}/leaf/picture").SetArgDisplayNames(
            "GetAvatarPresetPicture");
    }

    private static IEnumerable<TestCaseData> AdminEndpoints()
    {
        string path = $"/api/v1/users/{Guid.NewGuid()}/avatar";
        yield return new TestCaseData("PUT", path).SetArgDisplayNames("SetUserAvatar");
        yield return new TestCaseData("DELETE", path).SetArgDisplayNames("DeleteUserAvatar");
    }

    [TestCaseSource(nameof(SessionEndpoints))]
    [TestCaseSource(nameof(AdminEndpoints))]
    public async Task AvatarEndpoints_WithoutToken_ReturnUnauthorized(string method, string path)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), path)
        {
            Content = method == "PUT" ? Preset("leaf") : null,
        };

        HttpResponseMessage response = await _anonymous.SendAsync(request);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [TestCaseSource(nameof(AdminEndpoints))]
    public async Task AdminEndpoints_AsUser_ReturnForbidden(string method, string path)
    {
        (HttpClient user, _) = await CreateProfileAsync();
        using HttpRequestMessage request = new(new HttpMethod(method), path)
        {
            Content = method == "PUT" ? Preset("leaf") : null,
        };

        HttpResponseMessage response = await user.SendAsync(request);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Test]
    public async Task SetMyAvatar_Picture_IsDeliveredWithoutSessionUntilItIsRemoved()
    {
        (HttpClient client, UserDto user) = await CreateProfileAsync();
        string avatarPath = $"/api/v1/users/{user.Id}/avatar";

        HttpResponseMessage response = await client.PutAsync(MyAvatarPath, Upload(_picture));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        ProfileDto profile = (await response.Content.ReadFromJsonAsync<ProfileDto>(HttpClientExtensions.JsonOptions))!;
        profile.Id.Should().Be(user.Id);
        profile.Avatar.Should().NotBeNull();
        profile.Avatar!.Version.Should().NotBeNullOrEmpty();
        profile.Avatar.PresetId.Should().BeNull();
        (await FindProfileAsync(user.Id)).Avatar!.Version.Should().Be(profile.Avatar.Version);

        HttpResponseMessage picture = await _anonymous.GetAsync($"{avatarPath}?size=96");
        picture.StatusCode.Should().Be(HttpStatusCode.OK);
        picture.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        picture.Headers.CacheControl!.Private.Should().BeTrue();
        picture.Headers.CacheControl.NoCache.Should().BeTrue();
        picture.Content.Headers.LastModified.Should().NotBeNull();
        byte[] webp = await picture.Content.ReadAsByteArrayAsync();
        (await ImageProbe.GetSizeAsync(webp)).Should().Be((96, 96));
        using HttpRequestMessage again = new(HttpMethod.Get, $"{avatarPath}?size=96");
        again.Headers.IfNoneMatch.Add(picture.Headers.ETag!);
        (await _anonymous.SendAsync(again)).StatusCode.Should().Be(HttpStatusCode.NotModified);
        byte[] largest = await _anonymous.GetByteArrayAsync(avatarPath);
        (await ImageProbe.GetSizeAsync(largest)).Should().Be((250, 250));

        HttpResponseMessage removed = await client.DeleteAsync(MyAvatarPath);
        removed.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await FindProfileAsync(user.Id)).Avatar.Should().BeNull();
        await (await _anonymous.GetAsync(avatarPath)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "avatar.not_found");
        Directory.Exists(AvatarFolder(user.Id)).Should().BeFalse();
        (await client.DeleteAsync(MyAvatarPath)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task SetMyAvatar_NewPicture_ChangesVersionAndTag()
    {
        (HttpClient client, UserDto user) = await CreateProfileAsync();
        string avatarPath = $"/api/v1/users/{user.Id}/avatar?size=300";

        ProfileDto first = await SetAvatarAsync(client, Upload(_picture));
        EntityTagHeaderValue firstTag = (await _anonymous.GetAsync(avatarPath)).Headers.ETag!;
        ProfileDto second = await SetAvatarAsync(client, Upload(_picture));
        EntityTagHeaderValue secondTag = (await _anonymous.GetAsync(avatarPath)).Headers.ETag!;

        second.Avatar!.Version.Should().NotBe(first.Avatar!.Version);
        secondTag.Should().NotBe(firstTag);
    }

    [Test]
    public async Task SetMyAvatar_Preset_DeliversTheBuiltInPictureAndRemovesTheUpload()
    {
        (HttpClient client, UserDto user) = await CreateProfileAsync();
        await SetAvatarAsync(client, Upload(_picture));
        Directory.Exists(AvatarFolder(user.Id)).Should().BeTrue();

        ProfileDto profile = await SetAvatarAsync(client, Preset("leaf"));

        profile.Avatar!.PresetId.Should().Be("leaf");
        Directory.Exists(AvatarFolder(user.Id)).Should().BeFalse();
        HttpResponseMessage picture = await _anonymous.GetAsync($"/api/v1/users/{user.Id}/avatar?size=300");
        picture.StatusCode.Should().Be(HttpStatusCode.OK);
        byte[] expected = await (await client.GetAsync($"{PresetsPath}/leaf/picture?size=300"))
            .Content.ReadAsByteArrayAsync();
        (await picture.Content.ReadAsByteArrayAsync()).Should().Equal(expected);
    }

    [Test]
    public async Task SetMyAvatar_NeitherFileNorPreset_ReturnsValidationProblem()
    {
        (HttpClient client, _) = await CreateProfileAsync();
        MultipartFormDataContent empty = new() { { new StringContent("x"), "other" } };

        IReadOnlyList<ValidationErrorDto> errors = await (await client.PutAsync(MyAvatarPath, empty))
            .ShouldBeValidationProblemAsync();

        errors.Select(error => (error.Field, error.Code)).Should().BeEquivalentTo(
        [
            ("file", ValidationErrorCodeDto.Required),
            ("presetId", ValidationErrorCodeDto.Required),
        ]);
    }

    [Test]
    public async Task SetMyAvatar_FileAndPreset_ReturnsValidationProblem()
    {
        (HttpClient client, _) = await CreateProfileAsync();
        MultipartFormDataContent both = Upload(_picture);
        both.Add(new StringContent("leaf"), "presetId");

        IReadOnlyList<ValidationErrorDto> errors = await (await client.PutAsync(MyAvatarPath, both))
            .ShouldBeValidationProblemAsync();

        errors.Select(error => (error.Field, error.Code))
            .Should().Equal(("presetId", ValidationErrorCodeDto.InvalidValue));
    }

    [Test]
    public async Task SetMyAvatar_UnknownPreset_ReturnsValidationProblem()
    {
        (HttpClient client, _) = await CreateProfileAsync();

        IReadOnlyList<ValidationErrorDto> errors = await (await client.PutAsync(MyAvatarPath, Preset("unknown")))
            .ShouldBeValidationProblemAsync();

        errors.Select(error => (error.Field, error.Code))
            .Should().Equal(("presetId", ValidationErrorCodeDto.InvalidValue));
    }

    [Test]
    public async Task SetMyAvatar_NotAPicture_ReturnsUnsupportedFormat()
    {
        (HttpClient client, _) = await CreateProfileAsync();

        HttpResponseMessage response = await client.PutAsync(MyAvatarPath, Upload("just some text"u8.ToArray()));

        await response.ShouldBeProblemAsync(HttpStatusCode.UnsupportedMediaType, "avatar.unsupported_format");
    }

    [Test]
    public async Task SetMyAvatar_DamagedPicture_ReturnsUnreadableAndKeepsTheInitials()
    {
        (HttpClient client, UserDto user) = await CreateProfileAsync();
        byte[] damaged = [.. _picture.Take(64), .. new byte[2048]];

        HttpResponseMessage response = await client.PutAsync(MyAvatarPath, Upload(damaged));

        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "avatar.unreadable");
        (await FindProfileAsync(user.Id)).Avatar.Should().BeNull();
    }

    [Test]
    public async Task SetMyAvatar_TooLarge_ReturnsTooLarge()
    {
        (HttpClient client, _) = await CreateProfileAsync();
        byte[] large = [.. _picture, .. new byte[10 * 1024 * 1024]];

        HttpResponseMessage response = await client.PutAsync(MyAvatarPath, Upload(large));

        await response.ShouldBeProblemAsync(HttpStatusCode.RequestEntityTooLarge, "avatar.too_large");
    }

    [Test]
    public async Task SetUserAvatar_AsAdmin_SetsAndRemovesTheAvatarOfAnotherProfile()
    {
        (_, UserDto user) = await CreateProfileAsync();
        string path = $"/api/v1/users/{user.Id}/avatar";

        HttpResponseMessage response = await _admin.PutAsync(path, Preset("fish"));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        UserDto changed = (await response.Content.ReadFromJsonAsync<UserDto>(HttpClientExtensions.JsonOptions))!;
        changed.Id.Should().Be(user.Id);
        changed.Avatar!.PresetId.Should().Be("fish");
        (await _admin.GetJsonAsync<UserDto>($"/api/v1/users/{user.Id}")).Avatar!.PresetId.Should().Be("fish");

        HttpResponseMessage uploaded = await _admin.PutAsync(path, Upload(_picture));
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        (await uploaded.Content.ReadFromJsonAsync<UserDto>(HttpClientExtensions.JsonOptions))!.Avatar!.PresetId
            .Should().BeNull();

        (await _admin.DeleteAsync(path)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _admin.GetJsonAsync<UserDto>($"/api/v1/users/{user.Id}")).Avatar.Should().BeNull();
    }

    [Test]
    public async Task UserAvatarEndpoints_UnknownProfile_ReturnUserNotFound()
    {
        string path = $"/api/v1/users/{Guid.NewGuid()}/avatar";

        await (await _anonymous.GetAsync(path)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "user.not_found");
        await (await _admin.PutAsync(path, Preset("leaf")))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "user.not_found");
        await (await _admin.PutAsync(path, Upload(_picture)))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "user.not_found");
        await (await _admin.DeleteAsync(path)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "user.not_found");
    }

    [Test]
    public async Task GetUserAvatar_InvalidSize_ReturnsValidationProblem()
    {
        UserDto admin = await _admin.GetJsonAsync<UserDto>(
            $"/api/v1/users/{(await _anonymous.GetBuiltInAdminAsync()).Id}");

        await (await _anonymous.GetAsync($"/api/v1/users/{admin.Id}/avatar?size=0"))
            .ShouldBeValidationProblemAsync();
    }

    [Test]
    public async Task DeleteUser_WithUploadedAvatar_RemovesItsPictures()
    {
        (HttpClient client, UserDto user) = await CreateProfileAsync();
        await SetAvatarAsync(client, Upload(_picture));

        Directory.Exists(AvatarFolder(user.Id)).Should().BeTrue();

        (await _admin.DeleteAsync($"/api/v1/users/{user.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Directory.Exists(AvatarFolder(user.Id)).Should().BeFalse();
    }

    [Test]
    public async Task ListAvatarPresets_Always_ListsTheBuiltInAvatarsInOrder()
    {
        PageDto<AvatarPresetDto> page = await _admin.GetJsonAsync<PageDto<AvatarPresetDto>>(PresetsPath);

        page.Items.Select(preset => preset.Id)
            .Should().Equal("bubble", "leaf", "drop", "sun", "wave", "cloud", "star", "fish");
        page.Total.Should().Be(8);

        PageDto<AvatarPresetDto> second = await _admin.GetJsonAsync<PageDto<AvatarPresetDto>>(
            $"{PresetsPath}?offset=6&limit=5");
        second.Items.Select(preset => preset.Id).Should().Equal("star", "fish");
        second.Total.Should().Be(8);
    }

    [Test]
    public async Task GetAvatarPresetPicture_KnownPreset_DeliversItWithAnETag()
    {
        HttpResponseMessage picture = await _admin.GetAsync($"{PresetsPath}/bubble/picture?size=120");

        picture.StatusCode.Should().Be(HttpStatusCode.OK);
        picture.Content.Headers.ContentType!.MediaType.Should().Be("image/webp");
        (await ImageProbe.GetSizeAsync(await picture.Content.ReadAsByteArrayAsync())).Should().Be((300, 300));
        using HttpRequestMessage again = new(HttpMethod.Get, $"{PresetsPath}/bubble/picture?size=300");
        again.Headers.IfNoneMatch.Add(picture.Headers.ETag!);
        (await _admin.SendAsync(again)).StatusCode.Should().Be(HttpStatusCode.NotModified);
        await (await _admin.GetAsync($"{PresetsPath}/unknown/picture"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "avatar.not_found");
    }

    private async Task<(HttpClient Client, UserDto User)> CreateProfileAsync()
    {
        UserDto user = await _admin.CreateUserAsync(new { name = $"Avatars {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        HttpClient client = _factory.CreateAuthorizedClient(session.Token);
        _clients.Add(client);
        return (client, user);
    }

    private async Task<ProfileDto> FindProfileAsync(Guid id)
    {
        PageDto<ProfileDto> profiles = await _anonymous.GetJsonAsync<PageDto<ProfileDto>>("/api/v1/profiles?limit=100");
        return profiles.Items.Single(profile => profile.Id == id);
    }

    private string AvatarFolder(Guid userId)
    {
        return Path.Combine(_factory.DataPath, "avatars", userId.ToString("D"));
    }

    private static async Task<ProfileDto> SetAvatarAsync(HttpClient client, HttpContent content)
    {
        HttpResponseMessage response = await client.PutAsync(MyAvatarPath, content);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ProfileDto>(HttpClientExtensions.JsonOptions))!;
    }

    private static MultipartFormDataContent Upload(byte[] picture)
    {
        ByteArrayContent file = new(picture);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new MultipartFormDataContent { { file, "file", "avatar.png" } };
    }

    private static MultipartFormDataContent Preset(string id)
    {
        return new MultipartFormDataContent { { new StringContent(id), "presetId" } };
    }
}
