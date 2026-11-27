using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Policies;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Themes;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Uploaded themes: installing, replacing and deleting them as admin, listing them as any profile, the delivery of
/// their files without a session, the template and the Content Security Policy of the web app.
/// </summary>
[TestFixture]
[Category("Integration")]
[NonParallelizable]
public class ThemesApiTests
{
    private const int MaxUploadBytes = 64 * 1024;
    private const string RestartDatabaseName = "wavemp_tests_service_themes_restart";

    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _user = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new WaveMpWebApplicationFactory(
            "wavemp_tests_service_themes",
            freshDatabase: true,
            settings: new Dictionary<string, string?> { ["Themes:MaxUploadBytes"] = $"{MaxUploadBytes}" });
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        UserDto user = await _admin.CreateUserAsync(new { name = $"Listener {HttpClientExtensions.Unique()}" });
        _user = _factory.CreateAuthorizedClient((await _anonymous.CreateSessionAsync(user.Id)).Token);
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

    [SetUp]
    public async Task SetUp()
    {
        PageDto<ThemeDto> themes = await _admin.GetJsonAsync<PageDto<ThemeDto>>("/api/v1/themes");
        foreach (ThemeDto theme in themes.Items)
        {
            (await _admin.DeleteAsync($"/api/v1/themes/{theme.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
    }

    private static ByteArrayContent Zip(byte[] package)
    {
        ByteArrayContent content = new(package);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        return content;
    }

    private Task<HttpResponseMessage> InstallAsync(ThemePackageBuilder package, HttpClient? client = null)
    {
        return (client ?? _admin).PostAsync("/api/v1/themes", Zip(package.Build()));
    }

    private async Task<ThemeDto> InstallAuroraAsync()
    {
        HttpResponseMessage response = await InstallAsync(new ThemePackageBuilder());
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ThemeDto>(HttpClientExtensions.JsonOptions))!;
    }

    private static async Task<JsonArray> ShouldBeInvalidThemeAsync(HttpResponseMessage response)
    {
        await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity, "theme.invalid");
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["errors"]!.AsArray();
    }

    private static IEnumerable<TestCaseData> AdminEndpoints()
    {
        yield return new TestCaseData("POST", "/api/v1/themes").SetArgDisplayNames("InstallTheme");
        yield return new TestCaseData("PUT", "/api/v1/themes/aurora").SetArgDisplayNames("ReplaceTheme");
        yield return new TestCaseData("DELETE", "/api/v1/themes/aurora").SetArgDisplayNames("DeleteTheme");
    }

    [TestCaseSource(nameof(AdminEndpoints))]
    public async Task AdminEndpoints_WithoutToken_ReturnUnauthorized(string method, string path)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), path);

        HttpResponseMessage response = await _anonymous.SendAsync(request);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [TestCaseSource(nameof(AdminEndpoints))]
    public async Task AdminEndpoints_AsUser_ReturnForbidden(string method, string path)
    {
        using HttpRequestMessage request = new(new HttpMethod(method), path);
        request.Content = Zip(new ThemePackageBuilder().Build());

        HttpResponseMessage response = await _user.SendAsync(request);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Test]
    public async Task ListThemes_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage response = await _anonymous.GetAsync("/api/v1/themes");

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task InstallTheme_ValidPackage_IsListedForEveryProfile()
    {
        HttpResponseMessage response = await InstallAsync(new ThemePackageBuilder());

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        response.Headers.Location!.ToString().Should().EndWith("/api/v1/themes/aurora");
        ThemeDto theme = (await response.Content.ReadFromJsonAsync<ThemeDto>(HttpClientExtensions.JsonOptions))!;
        theme.Should().BeEquivalentTo(
            new
            {
                Id = "aurora",
                Name = "Aurora",
                Author = "Test Author",
                Version = "1.0.0",
                ThemeApi = 9,
                Variants = new[] { ThemeVariantDto.Light, ThemeVariantDto.Dark },
            });
        theme.StylesheetUrl.Should().MatchRegex("^/api/v1/themes/aurora/files/[0-9a-f]{32}\\.css$");
        theme.Previews.Select(preview => preview.Variant).Should().Equal(ThemeVariantDto.Light, ThemeVariantDto.Dark);
        theme.Status.Should().Be(ThemeStatusDto.Current);
        theme.Problems.Should().BeEmpty();

        PageDto<ThemeDto> list = await _user.GetJsonAsync<PageDto<ThemeDto>>("/api/v1/themes");
        list.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(theme);
        (await _user.GetJsonAsync<ThemeDto>("/api/v1/themes/aurora")).Should().BeEquivalentTo(theme);
    }

    [Test]
    public async Task GetThemeFile_WithoutToken_ReturnsTheRewrittenCssCachedForever()
    {
        ThemeDto theme = await InstallAuroraAsync();

        HttpResponseMessage response = await _anonymous.GetAsync(theme.StylesheetUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.ToString().Should().Be("text/css; charset=utf-8");
        response.Headers.CacheControl!.ToString().Should().Be("public, max-age=31536000, immutable");
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        response.Headers.GetValues("Content-Security-Policy").Should().Equal(ContentSecurityPolicies.ThemeFile);
        string css = await response.Content.ReadAsStringAsync();
        css.Should().MatchRegex("url\\(\"/api/v1/themes/aurora/files/[0-9a-f]{32}\\.svg\"\\)");

        using HttpRequestMessage again = new(HttpMethod.Get, theme.StylesheetUrl);
        again.Headers.IfNoneMatch.Add(response.Headers.ETag!);
        (await _anonymous.SendAsync(again)).StatusCode.Should().Be(HttpStatusCode.NotModified);
    }

    [Test]
    public async Task GetThemeFile_PicturesOfTheCss_AreServedWithTheirType()
    {
        ThemeDto theme = await InstallAuroraAsync();
        string css = await _anonymous.GetStringAsync(theme.StylesheetUrl);
        string svg = css[css.IndexOf("/api/v1/themes/aurora/files/", StringComparison.Ordinal)..];
        svg = svg[..svg.IndexOf('"', StringComparison.Ordinal)];

        HttpResponseMessage picture = await _anonymous.GetAsync(svg);
        HttpResponseMessage preview = await _anonymous.GetAsync(theme.Previews[0].Url);

        picture.Content.Headers.ContentType!.MediaType.Should().Be("image/svg+xml");
        preview.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
    }

    [Test]
    public async Task GetThemeFile_FontOfTheCss_IsServedAsWoff2()
    {
        HttpResponseMessage installed = await InstallAsync(new ThemePackageBuilder().WithFont());
        installed.StatusCode.Should().Be(HttpStatusCode.Created, await installed.Content.ReadAsStringAsync());
        ThemeDto theme = (await installed.Content.ReadFromJsonAsync<ThemeDto>(HttpClientExtensions.JsonOptions))!;
        string css = await _anonymous.GetStringAsync(theme.StylesheetUrl);
        string font = Regex.Match(css, "/api/v1/themes/aurora/files/[0-9a-f]{32}\\.woff2").Value;

        HttpResponseMessage response = await _anonymous.GetAsync(font);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("font/woff2");
        response.Headers.CacheControl!.ToString().Should().Be("public, max-age=31536000, immutable");
        response.Headers.GetValues("X-Content-Type-Options").Should().Equal("nosniff");
        (await response.Content.ReadAsByteArrayAsync()).Should().Equal(ThemePackageBuilder.Woff2());
    }

    [Test]
    public async Task InstallTheme_FontWithTheWrongContent_ReturnsFontInvalid()
    {
        ThemePackageBuilder package = new ThemePackageBuilder()
            .WithFont()
            .With("fonts/aurora.woff2", "<script>alert(1)</script>");

        JsonArray errors = await ShouldBeInvalidThemeAsync(await InstallAsync(package));

        errors.Select(error => (error!["code"]!.GetValue<string>(), error["path"]?.GetValue<string>()))
            .Should().Equal(("font.invalid", "fonts/aurora.woff2"));
    }

    [TestCase("/api/v1/themes/aurora/files/00000000000000000000000000000000.css")]
    [TestCase("/api/v1/themes/aurora/files/theme.json")]
    [TestCase("/api/v1/themes/aurora/files/..%2Ftheme.json")]
    [TestCase("/api/v1/themes/midnight/files/00000000000000000000000000000000.css")]
    public async Task GetThemeFile_OtherFile_ReturnsNotFound(string path)
    {
        await InstallAuroraAsync();

        HttpResponseMessage response = await _anonymous.GetAsync(path);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "theme.file_not_found");
    }

    [Test]
    public async Task InstallTheme_SameIdTwice_ReturnsConflict()
    {
        await InstallAuroraAsync();

        HttpResponseMessage response = await InstallAsync(new ThemePackageBuilder());

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "theme.already_installed");
    }

    [Test]
    public async Task InstallTheme_MaliciousPackage_ReturnsEveryReasonAsCode()
    {
        ThemePackageBuilder package = new ThemePackageBuilder()
            .With("theme.css", "@\\69mport 'x.css';\na { b: u\\72l(https://example.com/track.png); }")
            .With("images/evil.svg", """<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>""");

        JsonArray errors = await ShouldBeInvalidThemeAsync(await InstallAsync(package));

        errors.Select(error => (error!["code"]!.GetValue<string>(), error["path"]?.GetValue<string>()))
            .Should().BeEquivalentTo([
                ("svg.script", "images/evil.svg"),
                ("css.import", "theme.css"),
                ("css.external_url", "theme.css"),
            ]);
        errors.Should().AllSatisfy(error => error!.AsObject().Select(member => member.Key)
            .Should().BeSubsetOf(["code", "path", "line", "field", "file", "min", "max"]));
        (await _admin.GetJsonAsync<PageDto<ThemeDto>>("/api/v1/themes")).Items.Should().BeEmpty();
    }

    [Test]
    public async Task InstallTheme_ZipSlip_IsRejected()
    {
        JsonArray errors = await ShouldBeInvalidThemeAsync(
            await InstallAsync(new ThemePackageBuilder().With("../../escape.svg", ThemePackageBuilder.GlassSvg)));

        errors.Should().ContainSingle().Which!["code"]!.GetValue<string>().Should().Be("file.invalid_path");
        File.Exists(Path.Combine(_factory.DataPath, "escape.svg")).Should().BeFalse();
    }

    [Test]
    public async Task InstallTheme_NewerThemeApi_NamesTheSupportedVersions()
    {
        JsonArray errors = await ShouldBeInvalidThemeAsync(await InstallAsync(new ThemePackageBuilder(themeApi: 99)));

        errors.Should().ContainSingle().Which!.ToJsonString().Should().Be(
            """{"code":"manifest.unsupported_api_version","path":"manifest.json","field":"themeApi","min":"""
                + """5,"max":9}""");
    }

    [Test]
    public async Task InstallTheme_NoZip_ReturnsUnsupportedFormat()
    {
        HttpResponseMessage response = await _admin.PostAsync(
            "/api/v1/themes",
            new StringContent("a { color: red; }", Encoding.UTF8, "application/zip"));

        await response.ShouldBeProblemAsync(HttpStatusCode.UnsupportedMediaType, "theme.unsupported_format");
    }

    [Test]
    public async Task InstallTheme_LargerThanTheLimit_ReturnsTooLarge()
    {
        HttpResponseMessage response = await _admin.PostAsync("/api/v1/themes", Zip(new byte[MaxUploadBytes + 1]));

        await response.ShouldBeProblemAsync(HttpStatusCode.RequestEntityTooLarge, "theme.too_large");
    }

    [Test]
    public async Task ReplaceTheme_NewVersion_KeepsTheChoiceOfTheProfiles()
    {
        await InstallAuroraAsync();
        (await _user.PatchAsJsonAsync("/api/v1/me/settings", new { theme = new { id = "aurora" } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        HttpResponseMessage response = await _admin.PutAsync(
            "/api/v1/themes/aurora",
            Zip(new ThemePackageBuilder().WithManifest(ThemePackageBuilder.Manifest(name: "Aurora 2")).Build()));

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.Content.ReadFromJsonAsync<ThemeDto>(HttpClientExtensions.JsonOptions))!.Name
            .Should().Be("Aurora 2");
        (await _user.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings")).Theme.Id.Should().Be("aurora");
    }

    [Test]
    public async Task ListThemes_ThemeThatBreaksASecurityRuleOfANewerVersion_IsBlockedUntilItIsReplaced()
    {
        using TemporaryDirectory data = new();
        Dictionary<string, string?> settings = new() { ["DATA_PATH"] = data.Path };
        await using (WaveMpWebApplicationFactory first = new(RestartDatabaseName, settings: settings))
        {
            using HttpClient admin = await first.CreateAdminClientAsync();
            HttpResponseMessage installed = await admin.PostAsync(
                "/api/v1/themes",
                Zip(new ThemePackageBuilder().Build()));
            installed.StatusCode.Should().Be(HttpStatusCode.Created, await installed.Content.ReadAsStringAsync());
            (await admin.PatchAsJsonAsync("/api/v1/me/settings", new { theme = new { id = "aurora" } }))
                .StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // As if the version that installed the theme had let the script through.
        string svg = Directory.GetFiles(Path.Combine(data.Path, "themes", "aurora", "files"), "*.svg").Single();
        await File.WriteAllTextAsync(
            svg,
            """<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>""");

        await using WaveMpWebApplicationFactory second = new(RestartDatabaseName, settings: settings);
        using HttpClient restarted = await second.CreateAdminClientAsync();
        using HttpClient anonymous = second.CreateClient();
        PageDto<ThemeDto> list = await restarted.GetJsonAsync<PageDto<ThemeDto>>("/api/v1/themes");

        ThemeDto theme = list.Items.Should().ContainSingle().Subject;
        theme.Status.Should().Be(ThemeStatusDto.Blocked);
        theme.Problems.Should().BeEquivalentTo([new { Code = ThemeErrorCodeDto.SvgScript, Path = "images/glass.svg" }]);
        await (await anonymous.GetAsync(theme.StylesheetUrl))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "theme.file_not_found");
        foreach (ThemePreviewDto preview in theme.Previews)
        {
            (await anonymous.GetAsync(preview.Url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        // The profile keeps its choice; the clients show the default theme while the theme is blocked.
        (await restarted.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings")).Theme.Id.Should().Be("aurora");

        HttpResponseMessage replaced = await restarted.PutAsync(
            "/api/v1/themes/aurora",
            Zip(new ThemePackageBuilder().Build()));

        replaced.StatusCode.Should().Be(HttpStatusCode.OK, await replaced.Content.ReadAsStringAsync());
        ThemeDto current = (await restarted.GetJsonAsync<PageDto<ThemeDto>>("/api/v1/themes")).Items.Single();
        current.Status.Should().Be(ThemeStatusDto.Current);
        current.Problems.Should().BeEmpty();
        (await anonymous.GetAsync(current.StylesheetUrl)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task ListThemes_ThemeForAThemeApiVersionANewerVersionNoLongerSupports_IsOutdatedAndStillServed()
    {
        using TemporaryDirectory data = new();
        Dictionary<string, string?> settings = new() { ["DATA_PATH"] = data.Path };
        await using (WaveMpWebApplicationFactory first = new(RestartDatabaseName, settings: settings))
        {
            using HttpClient admin = await first.CreateAdminClientAsync();
            HttpResponseMessage installed = await admin.PostAsync(
                "/api/v1/themes",
                Zip(new ThemePackageBuilder().Build()));
            installed.StatusCode.Should().Be(HttpStatusCode.Created, await installed.Content.ReadAsStringAsync());
        }

        // As if the theme was written for a theme API version this version no longer supports.
        string description = Path.Combine(data.Path, "themes", "aurora", "theme.json");
        JsonObject json = JsonNode.Parse(await File.ReadAllTextAsync(description))!.AsObject();
        json["themeApi"] = 0;
        await File.WriteAllTextAsync(description, json.ToJsonString());

        await using WaveMpWebApplicationFactory second = new(RestartDatabaseName, settings: settings);
        using HttpClient restarted = await second.CreateAdminClientAsync();
        using HttpClient anonymous = second.CreateClient();
        ThemeDto theme = (await restarted.GetJsonAsync<PageDto<ThemeDto>>("/api/v1/themes")).Items
            .Should().ContainSingle().Subject;

        theme.Status.Should().Be(ThemeStatusDto.Outdated);
        theme.Problems.Should().ContainSingle().Which.Code.Should().Be(ThemeErrorCodeDto.UnsupportedApiVersion);
        (await anonymous.GetAsync(theme.StylesheetUrl)).StatusCode.Should().Be(HttpStatusCode.OK);
        foreach (ThemePreviewDto preview in theme.Previews)
        {
            (await anonymous.GetAsync(preview.Url)).StatusCode.Should().Be(HttpStatusCode.OK);
        }
    }

    [Test]
    public async Task ReplaceTheme_PackageOfAnotherTheme_IsRejected()
    {
        await InstallAuroraAsync();

        JsonArray errors = await ShouldBeInvalidThemeAsync(
            await _admin.PutAsync("/api/v1/themes/aurora", Zip(new ThemePackageBuilder("borealis").Build())));

        errors.Should().ContainSingle().Which!["code"]!.GetValue<string>().Should().Be("manifest.id_mismatch");
    }

    [Test]
    public async Task ReplaceTheme_MissingTheme_ReturnsNotFound()
    {
        HttpResponseMessage response = await _admin.PutAsync(
            "/api/v1/themes/aurora",
            Zip(new ThemePackageBuilder().Build()));

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "theme.not_found");
    }

    [Test]
    public async Task DeleteTheme_ChosenByAProfile_GivesItTheDefaultTheme()
    {
        ThemeDto theme = await InstallAuroraAsync();
        (await _user.PatchAsJsonAsync(
                "/api/v1/me/settings",
                new { theme = new { id = "aurora", appearance = "Dark" } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await _admin.DeleteAsync("/api/v1/themes/aurora")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        UserSettingsDto settings = await _user.GetJsonAsync<UserSettingsDto>("/api/v1/me/settings");
        settings.Theme.Id.Should().Be("midnight");
        settings.Theme.Appearance.Should().Be(ThemeAppearanceDto.Dark);
        await (await _anonymous.GetAsync(theme.StylesheetUrl))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "theme.file_not_found");
        await (await _admin.GetAsync("/api/v1/themes/aurora"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "theme.not_found");
    }

    [Test]
    public async Task DeleteTheme_MissingTheme_ReturnsNotFound()
    {
        await (await _admin.DeleteAsync("/api/v1/themes/aurora"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "theme.not_found");
    }

    [Test]
    public async Task GetThemeTemplate_WithoutToken_ReturnsAPackageThatInstalls()
    {
        HttpResponseMessage response = await _anonymous.GetAsync("/api/v1/theme-template");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("application/zip");
        response.Content.Headers.ContentDisposition!.FileNameStar.Should().Be("wavemp-theme-template.zip");
        byte[] template = await response.Content.ReadAsByteArrayAsync();
        using (ZipArchive archive = new(new MemoryStream(template), ZipArchiveMode.Read))
        {
            archive.Entries.Select(entry => entry.FullName).Should().Contain(["manifest.json", "theme.css"]);
        }

        HttpResponseMessage installed = await _admin.PostAsync("/api/v1/themes", Zip(template));

        installed.StatusCode.Should().Be(HttpStatusCode.Created, await installed.Content.ReadAsStringAsync());
    }

    [TestCase("/")]
    [TestCase("/library/albums")]
    public async Task WebApp_CarriesTheContentSecurityPolicy(string path)
    {
        HttpResponseMessage response = await _anonymous.GetAsync(path);

        response.Headers.GetValues("Content-Security-Policy").Should().Equal(ContentSecurityPolicies.WebApp);
    }

    [Test]
    public void ContentSecurityPolicy_OfTheWebApp_LoadsNothingFromOtherServersButTheCastSdk()
    {
        string[] sources = [.. ContentSecurityPolicies.WebApp.Split(' ', ';')
            .Where(source => source.Contains("://", StringComparison.Ordinal))
            .Distinct()];

        sources.Should().Equal("https://www.gstatic.com");
        ContentSecurityPolicies.WebApp.Should().Contain("style-src 'self' 'unsafe-inline';")
            .And.Contain("img-src 'self' data: blob:;")
            .And.Contain("font-src 'self';")
            .And.Contain("object-src 'none';");
    }

    [Test]
    public async Task Api_CarriesNoPolicyOfTheWebApp()
    {
        HttpResponseMessage response = await _user.GetAsync("/api/v1/themes");

        response.Headers.Contains("Content-Security-Policy").Should().BeFalse();
    }
}
