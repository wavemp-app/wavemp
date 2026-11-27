using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using WaveMP.Data;
using WaveMP.Data.Entities;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Seeds;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// The layout template of the organization: its settings and its preview, for admins only and with codes instead of
/// texts.
/// </summary>
[TestFixture]
[Category("Integration")]
public class OrganizingApiTests
{
    private const string SettingsPath = "/api/v1/settings/organizing";
    private const string PreviewPath = "/api/v1/organize/template-preview";
    private const string DefaultTemplate = "{AlbumArtist}/{Album}/{TrackNumber:00} {Title}";

    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _user = null!;
    private Track _faded = null!;
    private Track _hit = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new WaveMpWebApplicationFactory(
            "wavemp_tests_service_organizing",
            freshDatabase: true);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        UserDto user = await _admin.CreateUserAsync(new { name = $"Listener {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        _user = _factory.CreateAuthorizedClient(session.Token);

        LibrarySeed seed = new();
        Artist walker = seed.Artist("Alan Walker");
        Artist other = seed.Artist("AC/DC");
        Album world = seed.Album("Different World", walker, year: 2018);
        Album hits = seed.Album("Hits", isCompilation: true);
        _faded = seed.Track("Faded", walker, world, 3, genre: "Electronic");
        seed.File("Different World/0001 Faded.flac", MediaFileKind.Audio, _faded);
        seed.File("Different World/0001 Faded.mp4", MediaFileKind.Video, _faded);
        _hit = seed.Track("Thunder?", other, hits, 7, year: 1990);
        seed.File("Hits/0002 Thunder.mp3", MediaFileKind.Audio, _hit);
        await using AsyncServiceScope scope = _factory.Services.CreateAsyncScope();
        await seed.SaveAsync(scope.ServiceProvider.GetRequiredService<WaveMpDbContext>());
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
        await _admin.SendJsonAsync("PATCH", SettingsPath, $$"""{ "layoutTemplate": "{{DefaultTemplate}}" }""");
        await _admin.WaitForJobsIdleAsync();
    }

    [Test]
    public async Task GetOrganizingSettings_OfANewInstallation_IsTheFolderBasedDefault()
    {
        OrganizingSettingsDto settings = await _admin.GetJsonAsync<OrganizingSettingsDto>(SettingsPath);

        settings.Should().BeEquivalentTo(new OrganizingSettingsDto
        {
            LayoutTemplate = DefaultTemplate,
            IsFolderBased = true,
        });
    }

    [Test]
    public async Task UpdateOrganizingSettings_FileBasedTemplate_IsSavedTrimmed()
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PATCH",
            SettingsPath,
            """{ "layoutTemplate": "  {Artist} - {Title} " }""");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        OrganizingSettingsDto expected = new() { LayoutTemplate = "{Artist} - {Title}", IsFolderBased = false };
        (await response.Content.ReadFromJsonAsync<OrganizingSettingsDto>(HttpClientExtensions.JsonOptions))
            .Should().BeEquivalentTo(expected);
        (await _admin.GetJsonAsync<OrganizingSettingsDto>(SettingsPath)).Should().BeEquivalentTo(expected);
    }

    [TestCase("", ValidationErrorCodeDto.Required)]
    [TestCase("{Artist}/{Album}", ValidationErrorCodeDto.InvalidFormat)]
    [TestCase("{Composer} {Title}", ValidationErrorCodeDto.InvalidFormat)]
    public async Task UpdateOrganizingSettings_InvalidTemplate_ReturnsTheCodeAndKeepsTheSavedOne(
        string template,
        ValidationErrorCodeDto code)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PATCH",
            SettingsPath,
            $$"""{ "layoutTemplate": "{{template}}" }""");

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Select(error => (error.Field, error.Code)).Should().Equal(("layoutTemplate", code));
        (await _admin.GetJsonAsync<OrganizingSettingsDto>(SettingsPath)).LayoutTemplate.Should().Be(DefaultTemplate);
    }

    [Test]
    public async Task UpdateOrganizingSettings_TooLongTemplate_NamesTheLimit()
    {
        string template = "{Title}" + new string('x', 600);

        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PATCH",
            SettingsPath,
            $$"""{ "layoutTemplate": "{{template}}" }""");

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Select(error => (error.Field, error.Code, error.Max))
            .Should().Equal(("layoutTemplate", ValidationErrorCodeDto.MaxLength, (int?)512));
    }

    [Test]
    public async Task PreviewLayoutTemplate_WithoutSongs_ShowsTheFirstSongsOfTheLibrary()
    {
        LayoutTemplatePreviewDto preview = await PreviewAsync(new { template = DefaultTemplate });

        preview.IsValid.Should().BeTrue();
        preview.IsFolderBased.Should().BeTrue();
        preview.Errors.Should().BeEmpty();
        preview.Paths.Should().BeEquivalentTo(
            [
                new LayoutTemplatePathDto
                {
                    TrackId = _faded.Id,
                    Folders = ["Alan Walker", "Different World"],
                    BaseName = "03 Faded",
                    Extension = "flac",
                    IsTooLong = false,
                },
                new LayoutTemplatePathDto
                {
                    TrackId = _hit.Id,
                    Folders = ["Various Artists", "Hits"],
                    BaseName = "07 Thunder",
                    Extension = "mp3",
                    IsTooLong = false,
                },
            ],
            options => options.WithStrictOrdering());
    }

    [Test]
    public async Task PreviewLayoutTemplate_WithTracksAndSamples_ShowsThemInOrder()
    {
        LayoutTemplatePreviewDto preview = await PreviewAsync(new
        {
            template = "{Artist} - {Title}",
            trackIds = new[] { _hit.Id, Guid.NewGuid() },
            samples = new[] { new { title = "Song", artist = "Band", extension = "ogg" } },
        });

        preview.IsFolderBased.Should().BeFalse();
        preview.Paths.Select(path => (path.TrackId, path.Folders.Count, path.BaseName, path.Extension)).Should().Equal(
            (_hit.Id, 0, "AC_DC - Thunder", "mp3"),
            ((Guid?)null, 0, "Band - Song", "ogg"));
    }

    [Test]
    public async Task PreviewLayoutTemplate_InvalidTemplate_ReturnsItsProblemsAsCodes()
    {
        HttpResponseMessage response = await _admin.PostAsJsonAsync(
            PreviewPath,
            new { template = "{Composer}/{Album}" });
        string body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        body.Should().Contain("\"unknown_placeholder\"").And.Contain("\"missing_title\"");
        LayoutTemplatePreviewDto preview = (await response.Content
            .ReadFromJsonAsync<LayoutTemplatePreviewDto>(HttpClientExtensions.JsonOptions))!;
        preview.IsValid.Should().BeFalse();
        preview.Paths.Should().BeEmpty();
        preview.Errors.Should().BeEquivalentTo(
            [
                new LayoutTemplateErrorDto
                {
                    Code = LayoutTemplateErrorCodeDto.UnknownPlaceholder,
                    Position = 0,
                    Placeholder = "Composer",
                },
                new LayoutTemplateErrorDto { Code = LayoutTemplateErrorCodeDto.MissingTitle, Position = 11 },
            ]);
    }

    [Test]
    public async Task PreviewLayoutTemplate_EmptyTemplate_IsRequiredInThePreview()
    {
        LayoutTemplatePreviewDto preview = await PreviewAsync(new { template = string.Empty });

        preview.Errors.Select(error => error.Code).Should().Equal(LayoutTemplateErrorCodeDto.Required);
    }

    [Test]
    public async Task PreviewLayoutTemplate_TooManyTracks_ReturnsMaxItems()
    {
        HttpResponseMessage response = await _admin.PostAsJsonAsync(
            PreviewPath,
            new { template = DefaultTemplate, trackIds = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()) });

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle(error => error.Field == "trackIds");
    }

    [TestCase("GET", SettingsPath)]
    [TestCase("PATCH", SettingsPath)]
    [TestCase("POST", PreviewPath)]
    public async Task Organizing_AsListener_ReturnsForbidden(string method, string path)
    {
        HttpResponseMessage response = await _user.SendJsonAsync(
            method,
            path,
            method switch
            {
                "GET" => null,
                "PATCH" => """{ "layoutTemplate": "{Title}" }""",
                _ => """{ "template": "{Title}" }""",
            });

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [TestCase("GET", SettingsPath)]
    [TestCase("POST", PreviewPath)]
    public async Task Organizing_WithoutSession_ReturnsUnauthorized(string method, string path)
    {
        HttpResponseMessage response = await _anonymous.SendJsonAsync(
            method,
            path,
            method == "GET" ? null : """{ "template": "{Title}" }""");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private async Task<LayoutTemplatePreviewDto> PreviewAsync(object request)
    {
        HttpResponseMessage response = await _admin.PostAsJsonAsync(PreviewPath, request);
        string body = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return (await response.Content.ReadFromJsonAsync<LayoutTemplatePreviewDto>(HttpClientExtensions.JsonOptions))!;
    }
}
