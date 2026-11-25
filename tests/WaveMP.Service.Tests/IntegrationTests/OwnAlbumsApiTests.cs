using System.Net;
using System.Net.Http.Headers;
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
/// Own albums and the previews of organize operations, for admins only and with codes instead of texts.
/// </summary>
[TestFixture]
[Category("Integration")]
public class OwnAlbumsApiTests
{
    private const string AlbumsPath = "/api/v1/own-albums";
    private const string OrganizePath = "/api/v1/organize";

    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 1, 2, 3, 4];

    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _user = null!;
    private Track _first = null!;
    private Track _second = null!;
    private Track _clip = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_own_albums",
            freshDatabase: true);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        UserDto user = await _admin.CreateUserAsync(new { name = $"Listener {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        _user = _factory.CreateAuthorizedClient(session.Token);

        LibrarySeed seed = new();
        Artist artist = seed.Artist("Band");
        Album album = seed.Album("Record", artist, year: 2010);
        _first = seed.Track("First", artist, album, 1);
        seed.File("Record/0001 First.mp3", MediaFileKind.Audio, _first);
        _second = seed.Track("Second", artist, album, 2);
        seed.File("Record/0002 Second.flac", MediaFileKind.Audio, _second);
        _clip = seed.Track("Clip", artist, album, 3, hasAudio: false, hasVideo: true);
        seed.File("Record/0003 Clip.mkv", MediaFileKind.Video, _clip);
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

    [TestCase("GET", AlbumsPath)]
    [TestCase("POST", AlbumsPath)]
    [TestCase("GET", OrganizePath + "/operations")]
    [TestCase("GET", OrganizePath + "/availability")]
    [TestCase("POST", OrganizePath + "/preview")]
    [TestCase("POST", OrganizePath + "/apply")]
    [TestCase("POST", OrganizePath + "/operations/00000000-0000-0000-0000-000000000001/cancel")]
    public async Task Endpoints_WithoutSession_Return401AndForUsers403(string method, string path)
    {
        (await _anonymous.SendJsonAsync(method, path, "{}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await (await _user.SendJsonAsync(method, path, "{}"))
            .ShouldBeProblemAsync(HttpStatusCode.Forbidden, "auth.forbidden");
    }

    [Test]
    public async Task CreateOwnAlbum_ValidAlbum_IsCreatedChangedAndDeleted()
    {
        HttpResponseMessage created = await _admin.PostAsJsonAsync(
            AlbumsPath,
            new { title = " Summer Mix ", albumArtist = "Various", year = 2024 });

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        OwnAlbumDto album = (await created.Content.ReadFromJsonAsync<OwnAlbumDto>(HttpClientExtensions.JsonOptions))!;
        created.Headers.Location!.AbsolutePath.Should().Be($"{AlbumsPath}/{album.Id}");
        (album.Title, album.AlbumArtist, album.Year, album.Status, album.HasCover, album.TrackCount)
            .Should().Be(("Summer Mix", "Various", 2024, OwnAlbumStatusDto.Draft, false, 0));

        HttpResponseMessage changed = await _admin.SendJsonAsync(
            "PATCH",
            $"{AlbumsPath}/{album.Id}",
            """{ "title": "Winter Mix", "year": 0, "genre": "Pop" }""");
        changed.StatusCode.Should().Be(HttpStatusCode.OK);
        OwnAlbumDto updated = (await changed.Content.ReadFromJsonAsync<OwnAlbumDto>(HttpClientExtensions.JsonOptions))!;
        (updated.Title, updated.Year, updated.Genre).Should().Be(("Winter Mix", null, "Pop"));

        PageDto<OwnAlbumDto> page = await _admin.GetJsonAsync<PageDto<OwnAlbumDto>>($"{AlbumsPath}?sort=UpdatedAt");
        page.Items.Should().Contain(item => item.Id == album.Id);

        (await _admin.DeleteAsync($"{AlbumsPath}/{album.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await (await _admin.GetAsync($"{AlbumsPath}/{album.Id}"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "organize.own_album_not_found");
        await (await _admin.DeleteAsync($"{AlbumsPath}/{album.Id}"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "organize.own_album_not_found");
    }

    [TestCase("""{ "title": "", "albumArtist": "Various" }""", "title", ValidationErrorCodeDto.Required)]
    [TestCase("""{ "title": "   ", "albumArtist": "Various" }""", "title", ValidationErrorCodeDto.Required)]
    [TestCase("""{ "title": "Mix" }""", "albumArtist", ValidationErrorCodeDto.Required)]
    [TestCase("""{ "title": "Mix", "albumArtist": "Various", "year": 0 }""", "year", ValidationErrorCodeDto.OutOfRange)]
    public async Task CreateOwnAlbum_InvalidAlbum_ReturnsTheCode(string body, string field, ValidationErrorCodeDto code)
    {
        IReadOnlyList<ValidationErrorDto> errors = await (await _admin.SendJsonAsync("POST", AlbumsPath, body))
            .ShouldBeValidationProblemAsync();

        errors.Select(error => (error.Field, error.Code)).Should().Contain((field, code));
    }

    [Test]
    public async Task OwnAlbumTracks_AreAddedSortedListedAndChecked()
    {
        OwnAlbumDto album = await CreateAsync("Tracks");
        OwnAlbumDto other = await CreateAsync("Other");
        string tracks = $"{AlbumsPath}/{album.Id}/tracks";

        HttpResponseMessage added = await _admin.PostAsJsonAsync(
            tracks,
            new { trackIds = new[] { _second.Id, _first.Id } });
        added.StatusCode.Should().Be(HttpStatusCode.OK);
        (await added.Content.ReadFromJsonAsync<OwnAlbumDto>(HttpClientExtensions.JsonOptions))!.TrackCount.Should().Be(
            2);

        (await _admin.PutAsJsonAsync(tracks, new { trackIds = new[] { _first.Id, _second.Id } }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        PageDto<OwnAlbumTrackDto> page = await _admin.GetJsonAsync<PageDto<OwnAlbumTrackDto>>(tracks);
        page.Items.Select(item => (item.Position, item.Track.Id, item.State)).Should().Equal(
            (0, _first.Id, OwnAlbumTrackStateDto.Member),
            (1, _second.Id, OwnAlbumTrackStateDto.Member));

        await (await _admin.PostAsJsonAsync(tracks, new { trackIds = new[] { _clip.Id } }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "track.no_audio");
        await (await _admin.PostAsJsonAsync(tracks, new { trackIds = new[] { Guid.NewGuid() } }))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "track.not_found");
        await (await _admin.PostAsJsonAsync($"{AlbumsPath}/{other.Id}/tracks", new { trackIds = new[] { _first.Id } }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "organize.track_in_other_own_album");
        await (await _admin.PostAsJsonAsync(
                $"{AlbumsPath}/{Guid.NewGuid()}/tracks",
                new { trackIds = new[] { _first.Id } }))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "organize.own_album_not_found");

        (await _admin.DeleteAsync($"{tracks}/{_first.Id}")).StatusCode.Should().Be(HttpStatusCode.OK);
        await (await _admin.DeleteAsync($"{tracks}/{_first.Id}"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "track.not_found");
        await _admin.DeleteAsync($"{AlbumsPath}/{album.Id}");
        await _admin.DeleteAsync($"{AlbumsPath}/{other.Id}");
    }

    [Test]
    public async Task OwnAlbumCover_IsStoredDeliveredWithAnETagAndRemoved()
    {
        OwnAlbumDto album = await CreateAsync("Covered");
        string path = $"{AlbumsPath}/{album.Id}/cover";
        await (await _admin.GetAsync(path)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "cover.not_found");

        HttpResponseMessage saved = await _admin.PutAsync(path, Body(_png, "image/png"));
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        (await saved.Content.ReadFromJsonAsync<OwnAlbumDto>(HttpClientExtensions.JsonOptions))!
            .HasCover.Should().BeTrue();

        HttpResponseMessage cover = await _admin.GetAsync(path);
        cover.StatusCode.Should().Be(HttpStatusCode.OK);
        cover.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await cover.Content.ReadAsByteArrayAsync()).Should().Equal(_png);
        using HttpRequestMessage again = new(HttpMethod.Get, path);
        again.Headers.IfNoneMatch.Add(cover.Headers.ETag!);
        (await _admin.SendAsync(again)).StatusCode.Should().Be(HttpStatusCode.NotModified);

        await (await _admin.PutAsync(path, Body("no picture"u8.ToArray(), "image/png")))
            .ShouldBeProblemAsync(HttpStatusCode.UnsupportedMediaType, "cover.unsupported_format");
        await (await _admin.PutAsync($"{AlbumsPath}/{Guid.NewGuid()}/cover", Body(_png, "image/png")))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "organize.own_album_not_found");

        (await _admin.DeleteAsync(path)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await (await _admin.GetAsync(path)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "cover.not_found");
        await _admin.DeleteAsync($"{AlbumsPath}/{album.Id}");
    }

    [Test]
    public async Task CreateOrganizePreview_OfAnOwnAlbum_ChecksTheRequestAndTheAlbum()
    {
        IReadOnlyList<ValidationErrorDto> errors = await (await _admin.SendJsonAsync(
            "POST",
            $"{OrganizePath}/preview",
            """{ "kind": "OwnAlbum" }""")).ShouldBeValidationProblemAsync();
        errors.Select(error => (error.Field, error.Code)).Should().Equal(
            ("ownAlbumId", ValidationErrorCodeDto.Required));
        (await (await _admin.SendJsonAsync("POST", $"{OrganizePath}/preview", "{}")).ShouldBeValidationProblemAsync())
            .Select(error => error.Field).Should().Contain("kind");

        await (await _admin.PostAsJsonAsync(
                $"{OrganizePath}/preview",
                new { kind = "OwnAlbum", ownAlbumId = Guid.NewGuid() }))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "organize.own_album_not_found");
        OwnAlbumDto empty = await CreateAsync("Empty");
        await (await _admin.PostAsJsonAsync(
                $"{OrganizePath}/preview",
                new { kind = "OwnAlbum", ownAlbumId = empty.Id }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "organize.own_album_empty");
        await (await _admin.PostAsJsonAsync(
                $"{OrganizePath}/preview",
                new { kind = "Library", sourceId = Guid.NewGuid() }))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "source.not_found");
        await _admin.DeleteAsync($"{AlbumsPath}/{empty.Id}");
    }

    [Test]
    public async Task OrganizeOperations_APreviewIsListedWithItemsAndCanBeDiscarded()
    {
        OwnAlbumDto album = await CreateAsync("Planned");
        await _admin.PutAsJsonAsync($"{AlbumsPath}/{album.Id}/tracks", new { trackIds = new[] { _first.Id } });

        HttpResponseMessage response = await _admin.PostAsJsonAsync(
            $"{OrganizePath}/preview",
            new { kind = "OwnAlbum", ownAlbumId = album.Id });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        OrganizeOperationDto operation =
            (await response.Content.ReadFromJsonAsync<OrganizeOperationDto>(HttpClientExtensions.JsonOptions))!;
        response.Headers.Location!.AbsolutePath.Should().Be($"{OrganizePath}/operations/{operation.Id}");
        (operation.Kind, operation.Status, operation.OwnAlbumId, operation.TotalItems, operation.CanUndo)
            .Should().Be((OrganizeOperationKindDto.OwnAlbum, OrganizeOperationStatusDto.Previewed, album.Id, 1, false));

        PageDto<OrganizeItemDto> items = await _admin.GetJsonAsync<PageDto<OrganizeItemDto>>(
            $"{OrganizePath}/operations/{operation.Id}/items?filter=Conflicts");
        OrganizeItemDto item = items.Items.Single();
        (item.TrackId, item.ToDirectoryPath, item.ToBaseName).Should().Be((_first.Id, "Various/Planned", "01 First"));
        item.Conflict.Should().Be(OrganizeConflictDto.SourceMissing, "the seeded files do not exist on disk");
        item.TagChanges!.Fields.Should().Contain(OrganizeTagFieldDto.Album);
        item.Moves.Single().ToPath.Should().Be("Various/Planned/01 First.mp3");

        string json = await (await _admin.GetAsync($"{OrganizePath}/operations/{operation.Id}/items")).Content
            .ReadAsStringAsync();
        json.Should().Contain("\"conflict\":\"source_missing\"");

        PageDto<OrganizeOperationDto> log = await _admin.GetJsonAsync<PageDto<OrganizeOperationDto>>(
            $"{OrganizePath}/operations?kind=OwnAlbum&status=Previewed");
        log.Items.Select(entry => entry.Id).Should().Contain(operation.Id);
        await (await _admin.GetAsync($"{OrganizePath}/operations?status=Bogus")).ShouldBeValidationProblemAsync();
        await (await _admin.GetAsync($"{OrganizePath}/operations/{operation.Id}/items?filter=Bogus"))
            .ShouldBeValidationProblemAsync();

        await (await _admin.PostAsync($"{OrganizePath}/undo/{operation.Id}", null))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "organize.not_undoable");
        await (await _admin.PostAsync($"{OrganizePath}/undo/{Guid.NewGuid()}", null))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "organize.operation_not_found");

        (await _admin.DeleteAsync($"{OrganizePath}/operations/{operation.Id}"))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        await (await _admin.GetAsync($"{OrganizePath}/operations/{operation.Id}"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "organize.operation_not_found");
        await (await _admin.GetAsync($"{OrganizePath}/operations/{operation.Id}/items"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "organize.operation_not_found");
        await (await _admin.DeleteAsync($"{OrganizePath}/operations/{operation.Id}"))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "organize.operation_not_found");
        await _admin.DeleteAsync($"{AlbumsPath}/{album.Id}");
    }

    [Test]
    public async Task ApplyOrganizeOperation_ChecksThePreviewAndRunsItInTheBackground()
    {
        OwnAlbumDto album = await CreateAsync("Applied");
        await _admin.PutAsJsonAsync($"{AlbumsPath}/{album.Id}/tracks", new { trackIds = new[] { _second.Id } });
        OrganizeOperationDto preview = (await (await _admin.PostAsJsonAsync(
                $"{OrganizePath}/preview",
                new { kind = "OwnAlbum", ownAlbumId = album.Id }))
            .Content.ReadFromJsonAsync<OrganizeOperationDto>(HttpClientExtensions.JsonOptions))!;

        (await (await _admin.SendJsonAsync("POST", $"{OrganizePath}/apply", "{}")).ShouldBeValidationProblemAsync())
            .Select(error => (error.Field, error.Code)).Should().Equal(
                ("operationId", ValidationErrorCodeDto.Required));
        await (await _admin.PostAsJsonAsync($"{OrganizePath}/apply", new { operationId = Guid.NewGuid() }))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "organize.operation_not_found");
        await (await _admin.PostAsJsonAsync($"{OrganizePath}/apply", new { operationId = preview.Id }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "organize.conflicts");

        HttpResponseMessage response = await _admin.PostAsJsonAsync(
            $"{OrganizePath}/apply",
            new { operationId = preview.Id, skipConflicts = true });

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Headers.Location!.AbsolutePath.Should().Be($"{OrganizePath}/operations/{preview.Id}");
        OrganizeOperationDto queued =
            (await response.Content.ReadFromJsonAsync<OrganizeOperationDto>(HttpClientExtensions.JsonOptions))!;
        queued.SkipConflicts.Should().BeTrue();
        OrganizeOperationDto? finished = null;
        for (int attempt = 0; attempt < 300 && finished is null; attempt++)
        {
            OrganizeOperationDto current = await _admin.GetJsonAsync<OrganizeOperationDto>(
                $"{OrganizePath}/operations/{preview.Id}");
            if (current.Status is OrganizeOperationStatusDto.Queued or OrganizeOperationStatusDto.Running)
            {
                await Task.Delay(100);
                continue;
            }

            finished = current;
        }

        (finished!.Status, finished.SkippedItems, finished.DoneItems)
            .Should().Be((OrganizeOperationStatusDto.Completed, 1, 0), "the only song has a conflict and is skipped");
        await (await _admin.PostAsJsonAsync($"{OrganizePath}/apply", new { operationId = preview.Id }))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "organize.not_previewed");
        await (await _admin.PostAsync($"{OrganizePath}/operations/{preview.Id}/cancel", null))
            .ShouldBeProblemAsync(HttpStatusCode.Conflict, "organize.not_cancelable");
        await (await _admin.PostAsync($"{OrganizePath}/operations/{Guid.NewGuid()}/cancel", null))
            .ShouldBeProblemAsync(HttpStatusCode.NotFound, "organize.operation_not_found");
        await _admin.DeleteAsync($"{AlbumsPath}/{album.Id}");
    }

    [Test]
    public async Task GetOrganizeAvailability_WritableMediaFolder_IsAvailable()
    {
        OrganizeAvailabilityDto availability = await _admin.GetJsonAsync<OrganizeAvailabilityDto>(
            $"{OrganizePath}/availability");

        availability.IsAvailable.Should().BeTrue();
        availability.Sources.Should().ContainSingle().Which.Status.Should().Be(SourceAvailabilityStatusDto.Available);
        availability.ActiveOperationId.Should().BeNull();
    }

    private async Task<OwnAlbumDto> CreateAsync(string title)
    {
        HttpResponseMessage response = await _admin.PostAsJsonAsync(AlbumsPath, new { title, albumArtist = "Various" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<OwnAlbumDto>(HttpClientExtensions.JsonOptions))!;
    }

    private static ByteArrayContent Body(byte[] content, string contentType)
    {
        ByteArrayContent body = new(content);
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return body;
    }
}
