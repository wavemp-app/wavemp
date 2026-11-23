using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Song data after the folder of a song was moved or its files were renamed on disk: the next scan recognizes the song
/// at its new place and keeps its id, so the favorites of all profiles, the rating, the play history, the playlist
/// entries, the saved queue, the review state and the date added stay with it. Each test works on songs of its own.
/// The file watcher is off, so only the scans started by the tests change the library.
/// </summary>
[TestFixture]
[Category("Integration")]
public class SongDataCarryOverApiTests
{
    private const string Favorites = "/api/v1/me/favorites";

    private TemporaryDirectory _media = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _listener = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        await AudioAsync("Band/Record/01 Opening.mp3", "Band", "Record", "Opening", "1");
        await AudioAsync("Band/Record/02 Middle.mp3", "Band", "Record", "Middle", "2");
        await AudioAsync("Band/Record/03 Closing.mp3", "Band", "Record", "Closing", "3");
        await AudioAsync("Loose/Song.mp3", "Solo", "Single", "Loose Song", "1");
        await AudioAsync("Stays/Here.mp3", "Other", "Other", "Stays Here", "1");

        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_song_carry_over",
            freshDatabase: true,
            mediaPath: _media.Path);
        _anonymous = _factory.CreateClient();
        _admin = await _factory.CreateAdminClientAsync();
        UserDto user = await _admin.CreateUserAsync(new { name = $"Listener {HttpClientExtensions.Unique()}" });
        _listener = _factory.CreateAuthorizedClient((await _anonymous.CreateSessionAsync(user.Id)).Token);

        HttpResponseMessage settings = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/library",
            """{ "fileWatcherEnabled": false }""");
        settings.StatusCode.Should().Be(HttpStatusCode.OK, await settings.Content.ReadAsStringAsync());

        ScanDto scan = await ScanAsync();
        scan.Statistics!.TracksAdded.Should().Be(5);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _listener?.Dispose();
        _admin?.Dispose();
        _anonymous?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
        _media.Dispose();
    }

    [Test]
    public async Task Scan_FolderMovedIntoAnotherFolderAndRenamed_KeepsTheSongsWithAllTheirData()
    {
        TrackDto opening = await TrackAsync("Opening");
        TrackDto middle = await TrackAsync("Middle");
        TrackDto closing = await TrackAsync("Closing");
        Guid stays = (await TrackAsync("Stays Here")).Id;
        await MarkAsync(_admin, middle.Id);
        await MarkAsync(_listener, middle.Id);
        FavoriteDto adminMarked = await TrackFavoriteAsync(_admin, middle.Id);
        FavoriteDto listenerMarked = await TrackFavoriteAsync(_listener, middle.Id);
        await RateAsync(_listener, middle.Id, 4);
        await PlayAsync(_listener, middle.Id);
        await ReviewAsync(middle.Id);
        PlaylistDto playlist = await CreatePlaylistAsync(_listener, closing.Id, stays, middle.Id, opening.Id);
        await SaveQueueAsync(_listener, middle.Id, opening.Id, stays);
        TrackReviewStateDto reviewed =
            await _admin.GetJsonAsync<TrackReviewStateDto>($"/api/v1/tracks/{middle.Id}/review");
        IReadOnlyList<Guid> recentlyAdded = await RecentlyAddedIdsAsync();

        Directory.CreateDirectory(_media.Combine("Archive"));
        Directory.Move(_media.Combine("Band/Record"), _media.Combine("Archive/Band - Record (Remaster)"));
        Directory.Delete(_media.Combine("Band"));
        ScanDto scan = await ScanAsync();

        // The three songs are recognized at their new place, nothing is added or removed.
        (scan.Statistics!.TracksAdded, scan.Statistics.TracksRemoved).Should().Be((0, 0));
        TrackDto moved = await _listener.GetJsonAsync<TrackDto>($"/api/v1/tracks/{middle.Id}");
        (moved.Title, moved.IsFavorite, moved.Rating).Should().Be(("Middle", true, 4));
        moved.FirstSeenAt.Should().Be(middle.FirstSeenAt);
        (await TrackAsync("Opening")).Id.Should().Be(opening.Id);
        (await TrackAsync("Closing")).Id.Should().Be(closing.Id);
        (await RecentlyAddedIdsAsync()).Should().Equal(recentlyAdded);

        (await TrackFavoriteAsync(_admin, middle.Id)).AddedAt.Should().Be(adminMarked.AddedAt);
        (await TrackFavoriteAsync(_listener, middle.Id)).AddedAt.Should().Be(listenerMarked.AddedAt);
        PageDto<TrackDto> myMusic = await _listener.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?myMusic=true");
        myMusic.Items.Select(track => track.Id).Should().Contain(middle.Id);

        PageDto<RecentlyPlayedTrackDto> recentlyPlayed =
            await _listener.GetJsonAsync<PageDto<RecentlyPlayedTrackDto>>("/api/v1/me/recently-played");
        recentlyPlayed.Items.Select(item => item.Track.Id).Should().Equal(middle.Id);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ListeningStatisticsDto statistics = await _listener.GetJsonAsync<ListeningStatisticsDto>(
            $"/api/v1/me/stats?from={Day(now.AddDays(-1))}&to={Day(now)}&timeZone=UTC");
        statistics.TopTracks.Should().ContainSingle().Which.Should()
            .BeEquivalentTo(new { Track = new { middle.Id }, Plays = 1 });

        PageDto<PlaylistEntryDto> entries = await _listener.GetJsonAsync<PageDto<PlaylistEntryDto>>(
            $"/api/v1/me/playlists/{playlist.Id}/tracks?limit=100");
        entries.Items.Select(entry => entry.Track.Id).Should().Equal(closing.Id, stays, middle.Id, opening.Id);
        PlaybackStateDto queue = await _listener.GetJsonAsync<PlaybackStateDto>("/api/v1/me/playback-state");
        queue.Items.Select(item => item.Track.Id).Should().Equal(middle.Id, opening.Id, stays);

        (await _admin.GetJsonAsync<TrackReviewStateDto>($"/api/v1/tracks/{middle.Id}/review"))
            .Should().BeEquivalentTo(reviewed);
    }

    [Test]
    public async Task Scan_FilesOfASongRenamedInItsFolder_KeepsTheSongWithItsFavoritesAndRating()
    {
        TrackDto song = await TrackAsync("Loose Song");
        await MarkAsync(_admin, song.Id);
        await MarkAsync(_listener, song.Id);
        await RateAsync(_admin, song.Id, 5);
        await PlayAsync(_admin, song.Id);

        File.Move(_media.Combine("Loose/Song.mp3"), _media.Combine("Loose/01 - Renamed.mp3"));
        ScanDto scan = await ScanAsync();

        (scan.Statistics!.TracksAdded, scan.Statistics.TracksRemoved).Should().Be((0, 0));
        TrackDto kept = await _admin.GetJsonAsync<TrackDto>($"/api/v1/tracks/{song.Id}");
        (kept.Title, kept.IsFavorite, kept.Rating, kept.FirstSeenAt)
            .Should().Be(("Loose Song", true, 5, song.FirstSeenAt));
        (await TrackFavoriteAsync(_listener, song.Id)).ItemId.Should().Be(song.Id);
        PageDto<PlayHistoryEntryDto> history =
            await _admin.GetJsonAsync<PageDto<PlayHistoryEntryDto>>("/api/v1/me/history?limit=100");
        history.Items.Select(entry => entry.Track.Id).Should().Contain(song.Id);
    }

    private async Task<ScanDto> ScanAsync()
    {
        HttpResponseMessage response = await _admin.PostAsync("/api/v1/scans", null);
        response.StatusCode.Should().Be(HttpStatusCode.Accepted, await response.Content.ReadAsStringAsync());
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        ScanDto scan = await _admin.WaitForScanAsync(queued.Id);
        scan.Status.Should().Be(JobRunStatusDto.Succeeded);
        return scan;
    }

    private async Task<TrackDto> TrackAsync(string title)
    {
        PageDto<TrackDto> tracks = await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100");
        return tracks.Items.Single(track => track.Title == title);
    }

    private async Task<IReadOnlyList<Guid>> RecentlyAddedIdsAsync()
    {
        PageDto<TrackDto> tracks =
            await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?sort=FirstSeenAt&limit=100");
        return [.. tracks.Items.Select(track => track.Id)];
    }

    private async Task ReviewAsync(Guid trackId)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PUT",
            $"/api/v1/tracks/{trackId}/review",
            """{ "status": "Reviewed" }""");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task MarkAsync(HttpClient client, Guid trackId)
    {
        HttpResponseMessage response = await client.PutAsync($"{Favorites}/tracks/{trackId}", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task<FavoriteDto> TrackFavoriteAsync(HttpClient client, Guid trackId)
    {
        PageDto<FavoriteDto> favorites =
            await client.GetJsonAsync<PageDto<FavoriteDto>>($"{Favorites}?type=Track&limit=100");
        return favorites.Items.Single(favorite => favorite.ItemId == trackId);
    }

    private static async Task RateAsync(HttpClient client, Guid trackId, int rating)
    {
        HttpResponseMessage response = await client.PutAsJsonAsync($"/api/v1/tracks/{trackId}/rating", new { rating });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task PlayAsync(HttpClient client, Guid trackId)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/me/history",
            new { trackId, playedAt = DateTimeOffset.UtcNow.AddMinutes(-1) });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
    }

    private static async Task<PlaylistDto> CreatePlaylistAsync(HttpClient client, params Guid[] trackIds)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/me/playlists",
            new { name = "Moved songs", trackIds });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PlaylistDto>(HttpClientExtensions.JsonOptions))!;
    }

    private static async Task SaveQueueAsync(HttpClient client, params Guid[] trackIds)
    {
        HttpResponseMessage response = await client.PutAsJsonAsync(
            "/api/v1/me/playback-state",
            new
            {
                items = trackIds.Select(trackId => new { trackId }).ToArray(),
                currentIndex = 0,
                positionMilliseconds = 10_000,
                device = new { id = "phone", name = "Phone" },
            });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static string Day(DateTimeOffset moment)
    {
        return DateOnly.FromDateTime(moment.UtcDateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private Task AudioAsync(string path, string artist, string album, string title, string track)
    {
        return TestMediaGenerator.CreateAudioAsync(
            _media.Combine(path),
            AudioFormat.Mp3,
            new TestTags { Title = title, Artist = artist, Album = album, Track = track });
    }
}
