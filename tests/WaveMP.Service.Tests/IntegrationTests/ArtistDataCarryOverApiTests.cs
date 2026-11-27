using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Artist data after the metadata editor or the online search gave the songs of an artist another artist name: the
/// favorites of all profiles, the date added and a list in the player started from the artist go to the artist that got
/// the songs once the old artist has no songs left; so do its uploaded and its online picture (the online picture is
/// stored through the data layer, as the online lookup comes later). Each test works on artists of its own. The file
/// watcher is off, so only the API changes the library.
/// </summary>
[TestFixture]
[Category("Integration")]
public class ArtistDataCarryOverApiTests
{
    private const string Favorites = "/api/v1/me/favorites";
    private const string PlaybackState = "/api/v1/me/playback-state";

    private TemporaryDirectory _media = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _anonymous = null!;
    private HttpClient _admin = null!;
    private HttpClient _listener = null!;
    private byte[] _picture = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        var oldName = new TestTags { Artist = "Old Name", Album = "Record" };
        await AudioAsync("Old Name/Record/01.mp3", oldName with { Title = "Old One" });
        await AudioAsync("Old Name/Record/02.mp3", oldName with { Title = "Old Two" });
        await AudioAsync("Old Name/Single/01.mp3", oldName with { Album = "Single", Title = "Old Three" });
        var left = new TestTags { Artist = "Left", Album = "Left Album" };
        await AudioAsync("Left/Left Album/01.mp3", left with { Title = "Left One" });
        await AudioAsync("Left/Left Album/02.mp3", left with { Title = "Left Two" });
        await AudioAsync(
            "Right/Right Album/01.mp3",
            new TestTags { Artist = "Right", Album = "Right Album", Title = "Right One" });
        var pictured = new TestTags { Artist = "Pictured", Album = "Portraits" };
        await AudioAsync("Pictured/Portraits/01.mp3", pictured with { Title = "Pictured One" });
        await AudioAsync("Pictured/Portraits/02.mp3", pictured with { Title = "Pictured Two" });
        using (TemporaryDirectory upload = new())
        {
            await TestMediaGenerator.CreateImageAsync(upload.Combine("picture.png"), 320, 320);
            _picture = await File.ReadAllBytesAsync(upload.Combine("picture.png"));
        }

        var hosted = new TestTags { AlbumArtist = "Hosts", Album = "Night Mix" };
        await AudioAsync("Hosts/Night Mix/01.mp3", hosted with { Artist = "Singer A", Title = "Mix One" });
        await AudioAsync("Hosts/Night Mix/02.mp3", hosted with { Artist = "Singer B", Title = "Mix Two" });

        _factory = new WaveMpWebApplicationFactory(
            "wavemp_tests_service_artist_carry_over",
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
    public async Task UpdateTrackMetadata_ArtistOfEverySongOneByOne_DataFollowsOnceTheOldArtistIsEmpty()
    {
        TrackDto first = await TrackAsync("Old One");
        ArtistDto old = await _admin.GetJsonAsync<ArtistDto>($"/api/v1/artists/{first.Artist!.Id}");
        await MarkArtistAsync(_admin, old.Id);
        await MarkArtistAsync(_listener, old.Id);
        FavoriteDto adminMarked = await ArtistFavoriteAsync(_admin, old.Id);
        FavoriteDto listenerMarked = await ArtistFavoriteAsync(_listener, old.Id);
        await PlayFromArtistAsync(_listener, old.Id, first.Id);

        await RenameArtistAsync("Old One", "New Name");

        // A partial move carries nothing: the old artist still has songs and keeps its data.
        TrackDto moved = await TrackAsync("Old One");
        moved.Artist!.Name.Should().Be("New Name");
        (await ArtistFavoriteIdsAsync(_admin)).Should().Contain(old.Id).And.NotContain(moved.Artist.Id);
        (await ArtistFavoriteIdsAsync(_listener)).Should().Contain(old.Id).And.NotContain(moved.Artist.Id);
        (await _listener.GetJsonAsync<PlaybackStateDto>(PlaybackState)).Source!.Id.Should().Be(old.Id);

        await RenameArtistAsync("Old Two", "New Name");
        await RenameArtistAsync("Old Three", "New Name");

        (await _admin.GetAsync($"/api/v1/artists/{old.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        ArtistDto renamed = await _admin.GetJsonAsync<ArtistDto>($"/api/v1/artists/{moved.Artist.Id}");
        (renamed.Name, renamed.TrackCount, renamed.AlbumCount).Should().Be(("New Name", 3, 2));
        renamed.FirstSeenAt.Should().Be(old.FirstSeenAt);
        (await ArtistFavoriteAsync(_admin, renamed.Id)).AddedAt.Should().Be(adminMarked.AddedAt);
        (await ArtistFavoriteAsync(_listener, renamed.Id)).AddedAt.Should().Be(listenerMarked.AddedAt);
        (await ArtistFavoriteIdsAsync(_admin)).Should().NotContain(old.Id);
        (await ArtistFavoriteIdsAsync(_listener)).Should().NotContain(old.Id);
        (await _listener.GetJsonAsync<PlaybackStateDto>(PlaybackState)).Source
            .Should().BeEquivalentTo(new { Kind = PlaybackSourceKindDto.Artist, renamed.Id });
    }

    [Test]
    public async Task UpdateTrackMetadata_TwoArtistsJoinedUnderOneName_TheNewArtistGetsTheFavoritesOfBoth()
    {
        ArtistDto left = await _admin.GetJsonAsync<ArtistDto>(
            $"/api/v1/artists/{(await TrackAsync("Left One")).Artist!.Id}");
        ArtistDto right = await _admin.GetJsonAsync<ArtistDto>(
            $"/api/v1/artists/{(await TrackAsync("Right One")).Artist!.Id}");
        await MarkArtistAsync(_admin, left.Id);
        await MarkArtistAsync(_listener, right.Id);
        FavoriteDto adminMarked = await ArtistFavoriteAsync(_admin, left.Id);
        FavoriteDto listenerMarked = await ArtistFavoriteAsync(_listener, right.Id);

        await RenameArtistAsync("Left One", "Pair");
        await RenameArtistAsync("Left Two", "Pair");
        await RenameArtistAsync("Right One", "Pair");

        Guid pair = (await TrackAsync("Right One")).Artist!.Id;
        ArtistDto joined = await _admin.GetJsonAsync<ArtistDto>($"/api/v1/artists/{pair}");
        (joined.Name, joined.TrackCount).Should().Be(("Pair", 3));
        joined.FirstSeenAt.Should().Be(left.FirstSeenAt < right.FirstSeenAt ? left.FirstSeenAt : right.FirstSeenAt);
        (await _admin.GetAsync($"/api/v1/artists/{left.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _admin.GetAsync($"/api/v1/artists/{right.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ArtistFavoriteAsync(_admin, pair)).AddedAt.Should().Be(adminMarked.AddedAt);
        (await ArtistFavoriteAsync(_listener, pair)).AddedAt.Should().Be(listenerMarked.AddedAt);
        (await ArtistFavoriteIdsAsync(_admin)).Should().NotContain([left.Id, right.Id]);
        (await ArtistFavoriteIdsAsync(_listener)).Should().NotContain([left.Id, right.Id]);
    }

    [Test]
    public async Task UpdateTrackMetadata_ArtistWithPictures_UploadedAndOnlinePictureFollowTheRenamedArtist()
    {
        TrackDto first = await TrackAsync("Pictured One");
        Guid oldId = first.Artist!.Id;
        HttpResponseMessage upload = await _admin.PutAsync(
            $"/api/v1/artists/{oldId}/uploaded-picture",
            new MultipartFormDataContent { { new ByteArrayContent(_picture), "file", "picture.png" } });
        upload.StatusCode.Should().Be(HttpStatusCode.OK, await upload.Content.ReadAsStringAsync());
        ArtistDto old = (await upload.Content.ReadFromJsonAsync<ArtistDto>(HttpClientExtensions.JsonOptions))!;
        Guid onlineId = await _factory.AddOnlineArtistPictureAsync(oldId, _picture);

        await RenameArtistAsync("Pictured One", "Pictured Renamed");
        await RenameArtistAsync("Pictured Two", "Pictured Renamed");

        (await _admin.GetAsync($"/api/v1/artists/{oldId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        Guid renamedId = (await TrackAsync("Pictured One")).Artist!.Id;
        renamedId.Should().NotBe(oldId);
        ArtistDto renamed = await _listener.GetJsonAsync<ArtistDto>($"/api/v1/artists/{renamedId}");
        renamed.Picture.Should().BeEquivalentTo(new
        {
            Kind = ArtistPictureKindDto.Uploaded,
            old.Picture.Version,
            HasUploadedPicture = true,
            HasOnlinePicture = true,
        });
        renamed.Picture.OnlineCredit!.Author.Should().Be("Jane Doe");
        HttpResponseMessage shown = await _listener.GetAsync($"/api/v1/artists/{renamedId}/picture?size=96");
        shown.StatusCode.Should().Be(HttpStatusCode.OK);
        shown.Headers.ETag!.Tag.Should().StartWith($"\"{old.Picture.Version}-");
        HttpResponseMessage online = await _admin.GetAsync($"/api/v1/artists/{renamedId}/online-picture?size=96");
        online.StatusCode.Should().Be(HttpStatusCode.OK);
        online.Headers.ETag!.Tag.Should().StartWith($"\"o-{onlineId:N}-");
    }

    [Test]
    public async Task ApplyAlbumOnlineMetadata_AlbumArtistOfTheOnlyAlbum_TheNewAlbumArtistGetsTheFavorites()
    {
        TrackDto first = await TrackAsync("Mix One");
        AlbumDto album = await _admin.GetJsonAsync<AlbumDto>($"/api/v1/albums/{first.Album!.Id}");
        ArtistDto hosts = await _admin.GetJsonAsync<ArtistDto>($"/api/v1/artists/{album.Artist!.Id}");
        hosts.Name.Should().Be("Hosts");
        await MarkArtistAsync(_admin, hosts.Id);
        await MarkArtistAsync(_listener, hosts.Id);
        FavoriteDto adminMarked = await ArtistFavoriteAsync(_admin, hosts.Id);
        FavoriteDto listenerMarked = await ArtistFavoriteAsync(_listener, hosts.Id);
        await SetOnlineMetadataAsync(true);

        HttpResponseMessage response = await _admin.SendJsonAsync(
            "POST",
            $"/api/v1/albums/{album.Id}/online-metadata",
            """{ "tags": { "albumArtist": "Hosts United" } }""");
        await SetOnlineMetadataAsync(false);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        OnlineAlbumApplyResultDto result =
            (await response.Content.ReadFromJsonAsync<OnlineAlbumApplyResultDto>(HttpClientExtensions.JsonOptions))!;
        result.TracksWritten.Should().Be(2);
        AlbumDto applied = await _admin.GetJsonAsync<AlbumDto>($"/api/v1/albums/{result.AlbumId}");
        applied.Artist!.Name.Should().Be("Hosts United");
        (await _admin.GetAsync($"/api/v1/artists/{hosts.Id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        ArtistDto united = await _admin.GetJsonAsync<ArtistDto>($"/api/v1/artists/{applied.Artist.Id}");
        united.FirstSeenAt.Should().Be(hosts.FirstSeenAt);
        (await ArtistFavoriteAsync(_admin, united.Id)).AddedAt.Should().Be(adminMarked.AddedAt);
        (await ArtistFavoriteAsync(_listener, united.Id)).AddedAt.Should().Be(listenerMarked.AddedAt);
    }

    private async Task RenameArtistAsync(string title, string artist)
    {
        HttpResponseMessage edited = await _admin.SendJsonAsync(
            "PATCH",
            $"/api/v1/tracks/{(await TrackAsync(title)).Id}/metadata",
            $$"""{ "artist": "{{artist}}", "albumArtist": "{{artist}}" }""");
        edited.StatusCode.Should().Be(HttpStatusCode.OK, await edited.Content.ReadAsStringAsync());
    }

    private async Task<TrackDto> TrackAsync(string title)
    {
        PageDto<TrackDto> tracks = await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100");
        return tracks.Items.Single(track => track.Title == title);
    }

    private async Task SetOnlineMetadataAsync(bool enabled)
    {
        HttpResponseMessage response = await _admin.SendJsonAsync(
            "PATCH",
            "/api/v1/settings/online-metadata",
            enabled ? """{ "enabled": true }""" : """{ "enabled": false }""");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private static async Task PlayFromArtistAsync(HttpClient client, Guid artistId, Guid trackId)
    {
        HttpResponseMessage response = await client.PutAsJsonAsync(
            PlaybackState,
            new
            {
                items = new object[] { new { trackId } },
                currentIndex = 0,
                positionMilliseconds = 0,
                shuffle = false,
                originalOrder = new[] { 0 },
                repeat = "Off",
                source = new { kind = "Artist", id = artistId },
                device = new { id = "phone", name = "Phone" },
                changedAt = DateTimeOffset.UtcNow.AddSeconds(-1),
            });
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task MarkArtistAsync(HttpClient client, Guid artistId)
    {
        HttpResponseMessage response = await client.PutAsync($"{Favorites}/artists/{artistId}", null);
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task<FavoriteDto> ArtistFavoriteAsync(HttpClient client, Guid artistId)
    {
        PageDto<FavoriteDto> favorites =
            await client.GetJsonAsync<PageDto<FavoriteDto>>($"{Favorites}?type=Artist&limit=100");
        return favorites.Items.Single(favorite => favorite.ItemId == artistId);
    }

    private static async Task<IReadOnlyList<Guid>> ArtistFavoriteIdsAsync(HttpClient client)
    {
        PageDto<FavoriteDto> favorites =
            await client.GetJsonAsync<PageDto<FavoriteDto>>($"{Favorites}?type=Artist&limit=100");
        return [.. favorites.Items.Select(favorite => favorite.ItemId)];
    }

    private Task AudioAsync(string path, TestTags tags)
    {
        return TestMediaGenerator.CreateAudioAsync(_media.Combine(path), AudioFormat.Mp3, tags);
    }
}
