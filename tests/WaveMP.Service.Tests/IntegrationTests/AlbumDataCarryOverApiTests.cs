using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Album data after the metadata editor gave the songs of an album another album name: the favorites of all profiles,
/// the cover rule and the date added go to the album that got the songs once the old album has no songs left. Each
/// test works on albums of its own. The file watcher is off, so only the API changes the library.
/// </summary>
[TestFixture]
[Category("Integration")]
public class AlbumDataCarryOverApiTests
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
        await AudioAsync("Band/Old/01.mp3", "Band", "Old", "Song One");
        await AudioAsync("Band/Old/02.mp3", "Band", "Old", "Song Two");
        await AudioAsync("Band/Old/03.mp3", "Band", "Old", "Song Three");
        await AudioAsync("Duo/Early/01.mp3", "Duo", "Early", "Early One");
        await AudioAsync("Duo/Early/02.mp3", "Duo", "Early", "Early Two");
        await AudioAsync("Duo/Late/01.mp3", "Duo", "Late", "Late One");

        _factory = new WaveMpWebApplicationFactory(
            "wavemp_tests_service_album_carry_over",
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

        HttpResponseMessage response = await _admin.PostAsync("/api/v1/scans", null);
        ScanDto queued = (await response.Content.ReadFromJsonAsync<ScanDto>(HttpClientExtensions.JsonOptions))!;
        (await _admin.WaitForScanAsync(queued.Id)).Status.Should().Be(JobRunStatusDto.Succeeded);
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
    public async Task UpdateTrackMetadata_AlbumNameOfEverySongOneByOne_FavoritesAndCoverRuleFollowOnceOldAlbumIsEmpty()
    {
        TrackDto first = await TrackAsync("Song One");
        AlbumDto old = await _admin.GetJsonAsync<AlbumDto>($"/api/v1/albums/{first.Album!.Id}");
        await MarkAlbumAsync(_admin, old.Id);
        await MarkAlbumAsync(_listener, old.Id);
        await SetCoverRuleAsync(old.Id, "TrackFirst");
        FavoriteDto adminMarked = await AlbumFavoriteAsync(_admin, old.Id);
        FavoriteDto listenerMarked = await AlbumFavoriteAsync(_listener, old.Id);

        await RenameAlbumAsync("Song One", "New");

        // A partial move carries nothing: the old album still has songs and keeps its data.
        TrackDto moved = await TrackAsync("Song One");
        moved.Album!.Title.Should().Be("New");
        (await AlbumFavoriteIdsAsync(_admin)).Should().Contain(old.Id).And.NotContain(moved.Album.Id);
        (await AlbumFavoriteIdsAsync(_listener)).Should().Contain(old.Id).And.NotContain(moved.Album.Id);
        (await CoverRuleAsync(moved.Album.Id)).Rule.Should().Be(CoverRuleOptionDto.Inherit);

        await RenameAlbumAsync("Song Two", "New");
        await RenameAlbumAsync("Song Three", "New");

        (await _admin.GetAsync($"/api/v1/albums/{old.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        TrackDto last = await TrackAsync("Song Three");
        AlbumDto renamed = await _admin.GetJsonAsync<AlbumDto>($"/api/v1/albums/{last.Album!.Id}");
        (renamed.Id, renamed.Title, renamed.TrackCount).Should().Be((moved.Album.Id, "New", 3));
        renamed.FirstSeenAt.Should().Be(old.FirstSeenAt);
        (await CoverRuleAsync(renamed.Id)).Rule.Should().Be(CoverRuleOptionDto.TrackFirst);
        (await AlbumFavoriteAsync(_admin, renamed.Id)).AddedAt.Should().Be(adminMarked.AddedAt);
        (await AlbumFavoriteAsync(_listener, renamed.Id)).AddedAt.Should().Be(listenerMarked.AddedAt);
        (await AlbumFavoriteIdsAsync(_admin)).Should().NotContain(old.Id);
        (await AlbumFavoriteIdsAsync(_listener)).Should().NotContain(old.Id);
    }

    [Test]
    public async Task UpdateTrackMetadata_TwoAlbumsJoinedUnderOneName_TheNewAlbumGetsTheFavoritesOfBoth()
    {
        Guid early = (await TrackAsync("Early One")).Album!.Id;
        Guid late = (await TrackAsync("Late One")).Album!.Id;
        await MarkAlbumAsync(_admin, early);
        await MarkAlbumAsync(_listener, late);
        await SetCoverRuleAsync(early, "AlbumFirst");
        await SetCoverRuleAsync(late, "TrackFirst");

        await RenameAlbumAsync("Early One", "Joined");
        await RenameAlbumAsync("Early Two", "Joined");
        await RenameAlbumAsync("Late One", "Joined");

        Guid joined = (await TrackAsync("Late One")).Album!.Id;
        (await _admin.GetJsonAsync<AlbumDto>($"/api/v1/albums/{joined}")).TrackCount.Should().Be(3);
        (await AlbumFavoriteIdsAsync(_admin)).Should().Contain(joined).And.NotContain([early, late]);
        (await AlbumFavoriteIdsAsync(_listener)).Should().Contain(joined).And.NotContain([early, late]);

        // "Early" was left empty first and gave its rule to the new album; a rule the album already has is kept.
        (await CoverRuleAsync(joined)).Rule.Should().Be(CoverRuleOptionDto.AlbumFirst);
    }

    private async Task RenameAlbumAsync(string title, string album)
    {
        HttpResponseMessage edited = await _admin.SendJsonAsync(
            "PATCH",
            $"/api/v1/tracks/{(await TrackAsync(title)).Id}/metadata",
            $$"""{ "album": "{{album}}" }""");
        edited.StatusCode.Should().Be(HttpStatusCode.OK, await edited.Content.ReadAsStringAsync());
    }

    private async Task<TrackDto> TrackAsync(string title)
    {
        PageDto<TrackDto> tracks = await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100");
        return tracks.Items.Single(track => track.Title == title);
    }

    private async Task SetCoverRuleAsync(Guid albumId, string rule)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PUT",
            $"/api/v1/albums/{albumId}/cover-rule",
            $$"""{ "rule": "{{rule}}" }""");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private Task<CoverRuleDto> CoverRuleAsync(Guid albumId)
    {
        return _admin.GetJsonAsync<CoverRuleDto>($"/api/v1/albums/{albumId}/cover-rule");
    }

    private static async Task MarkAlbumAsync(HttpClient client, Guid albumId)
    {
        HttpResponseMessage response = await client.PutAsync($"{Favorites}/albums/{albumId}", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task<FavoriteDto> AlbumFavoriteAsync(HttpClient client, Guid albumId)
    {
        PageDto<FavoriteDto> favorites =
            await client.GetJsonAsync<PageDto<FavoriteDto>>($"{Favorites}?type=Album&limit=100");
        return favorites.Items.Single(favorite => favorite.ItemId == albumId);
    }

    private static async Task<IReadOnlyList<Guid>> AlbumFavoriteIdsAsync(HttpClient client)
    {
        PageDto<FavoriteDto> favorites =
            await client.GetJsonAsync<PageDto<FavoriteDto>>($"{Favorites}?type=Album&limit=100");
        return [.. favorites.Items.Select(favorite => favorite.ItemId)];
    }

    private Task AudioAsync(string path, string artist, string album, string title)
    {
        return TestMediaGenerator.CreateAudioAsync(
            _media.Combine(path),
            AudioFormat.Mp3,
            new TestTags { Title = title, Artist = artist, Album = album });
    }
}
