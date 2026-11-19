using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Saves and reads the playback state of profiles through the API, with a small library of synthetic media: the
/// state follows the profile to another session, and the most recently changed state wins.
/// </summary>
[TestFixture]
[Category("Integration")]
public class PlaybackStateApiTests
{
    private const string Path = "/api/v1/me/playback-state";

    private TemporaryDirectory _media = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private IReadOnlyList<TrackDto> _tracks = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        foreach (string title in new[] { "First", "Second", "Third" })
        {
            await TestMediaGenerator.CreateAudioAsync(
                _media.Combine($"{title}.mp3"),
                AudioFormat.Mp3,
                new TestTags { Title = title });
        }

        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_playback_state",
            freshDatabase: true,
            mediaPath: _media.Path);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();

        HttpResponseMessage response = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        ScanDto scan = await _admin.WaitForScanAsync(queued.Id);
        scan.Status.Should().Be(JobRunStatusDto.Succeeded);

        _tracks = (await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100")).Items;
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
        _media.Dispose();
    }

    [Test]
    public async Task PlaybackState_WithoutToken_ReturnsUnauthorized()
    {
        HttpResponseMessage read = await _anonymous.GetAsync(Path);
        HttpResponseMessage saved = await _anonymous.PutAsJsonAsync(Path, State("phone", [Track("First")], 0));

        await read.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
        await saved.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "auth.unauthorized");
    }

    [Test]
    public async Task GetMyPlaybackState_NothingSaved_ReturnsNotFound()
    {
        using HttpClient client = (await CreateProfileAsync()).Client;

        HttpResponseMessage response = await client.GetAsync(Path);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "playback.state_not_found");
    }

    [Test]
    public async Task SaveMyPlaybackState_ThenGet_ReturnsTheStateWithItsSongs()
    {
        using HttpClient client = (await CreateProfileAsync()).Client;
        var album = Guid.NewGuid();
        DateTimeOffset changedAt = DateTimeOffset.UtcNow.AddSeconds(-5);
        changedAt = new DateTimeOffset(changedAt.Ticks - (changedAt.Ticks % TimeSpan.TicksPerSecond), TimeSpan.Zero);

        HttpResponseMessage response = await client.PutAsJsonAsync(
            Path,
            new
            {
                items = new object[]
                {
                    new { trackId = Track("Second") },
                    new { trackId = Track("First"), autoplay = false },
                    new { trackId = Track("Third"), autoplay = true },
                },
                currentIndex = 1,
                positionMilliseconds = 42_500,
                shuffle = true,
                originalOrder = new[] { 1, 0, 2 },
                repeat = "All",
                source = new { kind = "Album", id = album },
                device = new { id = "phone", name = "Safari · iPhone" },
                changedAt,
            });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        PlaybackStateDto state = await client.GetJsonAsync<PlaybackStateDto>(Path);
        state.Items.Select(item => item.Track.Title).Should().Equal("Second", "First", "Third");
        state.Items.Select(item => item.Autoplay).Should().Equal(false, false, true);
        state.CurrentIndex.Should().Be(1);
        state.PositionMilliseconds.Should().Be(42_500);
        state.Shuffle.Should().BeTrue();
        state.OriginalOrder.Should().Equal(1, 0, 2);
        state.Repeat.Should().Be(RepeatModeDto.All);
        state.Source.Should().BeEquivalentTo(new { Kind = PlaybackSourceKindDto.Album, Id = album });
        state.Device.Should().BeEquivalentTo(new { Id = "phone", Name = "Safari · iPhone" });
        state.ChangedAt.Should().Be(changedAt);
    }

    [Test]
    public async Task GetMyPlaybackState_OnAnotherDevice_ReturnsTheStateOfTheProfile()
    {
        Profile profile = await CreateProfileAsync();
        using HttpClient phone = profile.Client;
        using HttpClient laptop = _factory.CreateAuthorizedClient((await _anonymous.CreateSessionAsync(profile.Id)).Token);
        using HttpClient other = (await CreateProfileAsync()).Client;
        await SaveAsync(phone, State("phone", [Track("First"), Track("Second")], 1));

        PlaybackStateDto state = await laptop.GetJsonAsync<PlaybackStateDto>(Path);
        HttpResponseMessage foreign = await other.GetAsync(Path);

        state.Items.Select(item => item.Track.Title).Should().Equal("First", "Second");
        state.CurrentIndex.Should().Be(1);
        state.Device.Id.Should().Be("phone");
        await foreign.ShouldBeProblemAsync(HttpStatusCode.NotFound, "playback.state_not_found");
    }

    [Test]
    public async Task SaveMyPlaybackState_OlderThanTheSavedState_ReturnsConflictAndKeepsIt()
    {
        using HttpClient client = (await CreateProfileAsync()).Client;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await SaveAsync(client, State("phone", [Track("First")], 0, now.AddSeconds(-5)));

        HttpResponseMessage response = await client.PutAsJsonAsync(
            Path,
            State("laptop", [Track("Second")], 0, now.AddSeconds(-10)));

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "playback.state_outdated");
        PlaybackStateDto state = await client.GetJsonAsync<PlaybackStateDto>(Path);
        state.Device.Id.Should().Be("phone");
        state.Items.Single().Track.Title.Should().Be("First");
    }

    [Test]
    public async Task SaveMyPlaybackState_ChangedLaterOnAnotherDevice_WinsOverTheSavedState()
    {
        using HttpClient client = (await CreateProfileAsync()).Client;
        await SaveAsync(client, State("phone", [Track("First")], 0, DateTimeOffset.UtcNow.AddSeconds(-5)));

        await SaveAsync(client, State("laptop", [Track("Third")], 0, changedAt: null));

        PlaybackStateDto state = await client.GetJsonAsync<PlaybackStateDto>(Path);
        state.Device.Id.Should().Be("laptop");
        state.Items.Single().Track.Title.Should().Be("Third");
        state.ChangedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(30));
    }

    [Test]
    public async Task SaveMyPlaybackState_ChangedAtInTheFuture_CountsAsNow()
    {
        using HttpClient client = (await CreateProfileAsync()).Client;
        await SaveAsync(client, State("phone", [Track("First")], 0, DateTimeOffset.UtcNow.AddHours(1)));

        await SaveAsync(client, State("laptop", [Track("Second")], 0, changedAt: null));

        (await client.GetJsonAsync<PlaybackStateDto>(Path)).Device.Id.Should().Be("laptop");
    }

    [Test]
    public async Task GetMyPlaybackState_UnknownSongs_LeavesThemOut()
    {
        using HttpClient client = (await CreateProfileAsync()).Client;
        await SaveAsync(client, State("phone", [Guid.NewGuid(), Track("First"), Guid.NewGuid()], 2));

        PlaybackStateDto state = await client.GetJsonAsync<PlaybackStateDto>(Path);

        state.Items.Select(item => item.Track.Title).Should().Equal("First");
        state.CurrentIndex.Should().Be(0);
        state.PositionMilliseconds.Should().Be(0);
    }

    [Test]
    public async Task SaveMyPlaybackState_EmptyQueue_IsSaved()
    {
        using HttpClient client = (await CreateProfileAsync()).Client;

        await SaveAsync(client, State("phone", [], -1));

        PlaybackStateDto state = await client.GetJsonAsync<PlaybackStateDto>(Path);
        state.Items.Should().BeEmpty();
        state.CurrentIndex.Should().Be(-1);
        state.Source.Should().BeNull();
        state.Repeat.Should().Be(RepeatModeDto.Off);
    }

    [Test]
    public async Task SaveMyPlaybackState_CurrentIndexOutsideTheQueue_ReturnsOutOfRange()
    {
        using HttpClient client = (await CreateProfileAsync()).Client;

        HttpResponseMessage response = await client.PutAsJsonAsync(
            Path,
            State("phone", [Track("First"), Track("Second")], 2));

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Field = "currentIndex", Code = ValidationErrorCodeDto.OutOfRange, Min = -1, Max = 1 });
    }

    [Test]
    public async Task SaveMyPlaybackState_BrokenOrderBeforeShuffling_ReturnsInvalidValue()
    {
        using HttpClient client = (await CreateProfileAsync()).Client;

        HttpResponseMessage response = await client.PutAsJsonAsync(
            Path,
            new
            {
                items = new[] { new { trackId = Track("First") }, new { trackId = Track("Second") } },
                currentIndex = 0,
                positionMilliseconds = 0,
                shuffle = true,
                originalOrder = new[] { 0, 0 },
                device = new { id = "phone", name = "Phone" },
            });

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Field = "originalOrder", Code = ValidationErrorCodeDto.InvalidValue });
    }

    [Test]
    public async Task SaveMyPlaybackState_WithoutDeviceAndPosition_ReturnsRequired()
    {
        using HttpClient client = (await CreateProfileAsync()).Client;

        HttpResponseMessage response = await client.PutAsJsonAsync(
            Path,
            new { items = Array.Empty<object>(), currentIndex = -1 });

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        (string Field, ValidationErrorCodeDto Code)[] expected =
        [
            ("device", ValidationErrorCodeDto.Required),
            ("positionMilliseconds", ValidationErrorCodeDto.Required),
        ];
        errors.Select(error => (error.Field, error.Code)).Should().BeEquivalentTo(expected);
    }

    [Test]
    public async Task SaveMyPlaybackState_TooManySongs_ReturnsMaxItems()
    {
        using HttpClient client = (await CreateProfileAsync()).Client;
        Guid[] tracks = [.. Enumerable.Repeat(Track("First"), 2001)];

        HttpResponseMessage response = await client.PutAsJsonAsync(Path, State("phone", tracks, 0));

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Field = "items", Code = ValidationErrorCodeDto.MaxItems, Max = 2000 });
    }

    [Test]
    public async Task SaveMyPlaybackState_NegativePosition_ReturnsOutOfRange()
    {
        using HttpClient client = (await CreateProfileAsync()).Client;

        HttpResponseMessage response = await client.PutAsJsonAsync(
            Path,
            new
            {
                items = new[] { new { trackId = Track("First") } },
                currentIndex = 0,
                positionMilliseconds = -1,
                device = new { id = "phone", name = "Phone" },
            });

        IReadOnlyList<ValidationErrorDto> errors = await response.ShouldBeValidationProblemAsync();
        errors.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { Field = "positionMilliseconds", Code = ValidationErrorCodeDto.OutOfRange });
    }

    private static object State(
        string device,
        IEnumerable<Guid> trackIds,
        int currentIndex,
        DateTimeOffset? changedAt = null)
    {
        return new
        {
            items = trackIds.Select(trackId => new { trackId }).ToArray(),
            currentIndex,
            positionMilliseconds = 30_000,
            device = new { id = device, name = "Device " + device },
            changedAt,
        };
    }

    private static async Task SaveAsync(HttpClient client, object state)
    {
        HttpResponseMessage response = await client.PutAsJsonAsync(Path, state);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private Guid Track(string title)
    {
        return _tracks.Single(track => track.Title == title).Id;
    }

    private async Task<Profile> CreateProfileAsync()
    {
        UserDto user = await _admin.CreateUserAsync(new { name = $"Playback {HttpClientExtensions.Unique()}" });
        CreatedSessionDto session = await _anonymous.CreateSessionAsync(user.Id);
        return new Profile(user.Id, _factory.CreateAuthorizedClient(session.Token));
    }

    private sealed record Profile(Guid Id, HttpClient Client);
}
