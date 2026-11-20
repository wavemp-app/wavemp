using System.Net;
using System.Net.Http.Json;
using AwesomeAssertions;
using WaveMP.Service.Dtos;
using WaveMP.Service.Tests.IntegrationTests.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Service.Tests.IntegrationTests;

/// <summary>
/// Albums and covers after the metadata editor changed the artist of songs: songs without artist in the folder of the
/// media source (listed under an unknown artist), one of them with a picture file of its own, and an unrelated album
/// of the same name with a cover in its folder. Every song shows its own cover, the cover of its album or none, never
/// the cover of another song. The file watcher is off, so only the API changes the library.
/// </summary>
[TestFixture]
[Category("Integration")]
public class AlbumIdentityApiTests
{
    private TemporaryDirectory _media = null!;
    private WaveMpWebApplicationFactory _factory = null!;
    private HttpClient _admin = null!;
    private byte[] _ownPicture = null!;
    private byte[] _folderPicture = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _media = new TemporaryDirectory();
        await AudioAsync("Alpha.mp3", new TestTags { Title = "Alpha", Album = "Hits" });
        await AudioAsync("Beta.mp3", new TestTags { Title = "Beta", Album = "Hits" });
        await AudioAsync("Gamma.mp3", new TestTags { Title = "Gamma", Album = "Hits" });
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Gamma.jpg"), 120, 120);
        await AudioAsync("Various/Hits/01.mp3", new TestTags { Title = "X", Artist = "X", Album = "Hits" });
        await AudioAsync("Various/Hits/02.mp3", new TestTags { Title = "Y", Artist = "Y", Album = "Hits" });
        await TestMediaGenerator.CreateImageAsync(_media.Combine("Various/Hits/cover.jpg"), 200, 200);
        _ownPicture = await File.ReadAllBytesAsync(_media.Combine("Gamma.jpg"));
        _folderPicture = await File.ReadAllBytesAsync(_media.Combine("Various/Hits/cover.jpg"));

        _factory = new WaveMpWebApplicationFactory(
            WaveMpWebApplicationFactory.TestDatabaseName + "_album_identity",
            freshDatabase: true,
            mediaPath: _media.Path);
        _admin = await _factory.CreateAdminClientAsync();

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
        _admin?.Dispose();
        await _factory.DeleteDatabaseAsync();
        await _factory.DisposeAsync();
        _media.Dispose();
    }

    [Test]
    public async Task UpdateTrackMetadata_ArtistOfSeveralSongs_EverySongShowsItsOwnCoverItsAlbumCoverOrNone()
    {
        foreach ((string title, string artist) in new[] { ("Alpha", "Ann"), ("Beta", "Bob"), ("Gamma", "Gina") })
        {
            HttpResponseMessage edited = await _admin.SendJsonAsync(
                "PATCH",
                $"/api/v1/tracks/{(await TrackAsync(title)).Id}/metadata",
                $$"""{ "artist": "{{artist}}" }""");
            edited.StatusCode.Should().Be(HttpStatusCode.OK, await edited.Content.ReadAsStringAsync());
        }

        TrackDto alpha = await TrackAsync("Alpha");
        TrackDto beta = await TrackAsync("Beta");
        TrackDto gamma = await TrackAsync("Gamma");
        TrackDto x = await TrackAsync("X");
        TrackDto y = await TrackAsync("Y");

        (alpha.Album!.Title, alpha.Album.Artist!.Name).Should().Be(("Hits", "Ann"));
        (beta.Album!.Title, beta.Album.Artist!.Name).Should().Be(("Hits", "Bob"));
        (gamma.Album!.Title, gamma.Album.Artist!.Name).Should().Be(("Hits", "Gina"));
        new[] { alpha, beta, gamma, x, y }.Select(track => track.Album!.Id).Should().OnlyHaveUniqueItems();

        (alpha.HasCover, beta.HasCover, gamma.HasCover, x.HasCover, y.HasCover)
            .Should().Be((false, false, true, true, true));
        await CoverShouldBeMissingAsync($"/api/v1/tracks/{alpha.Id}/cover");
        await CoverShouldBeMissingAsync($"/api/v1/tracks/{beta.Id}/cover");
        await CoverShouldBeMissingAsync($"/api/v1/albums/{alpha.Album.Id}/cover");
        (await CoverAsync($"/api/v1/tracks/{gamma.Id}/cover")).Should().Equal(_ownPicture);
        (await CoverAsync($"/api/v1/albums/{gamma.Album.Id}/cover")).Should().Equal(_ownPicture);
        (await CoverAsync($"/api/v1/tracks/{x.Id}/cover")).Should().Equal(_folderPicture);
        (await CoverAsync($"/api/v1/tracks/{y.Id}/cover")).Should().Equal(_folderPicture);

        PageDto<AlbumDto> albums = await _admin.GetJsonAsync<PageDto<AlbumDto>>("/api/v1/albums?limit=100");
        albums.Items.Select(album => (album.Title, album.Artist!.Name, album.TrackCount)).Should().BeEquivalentTo(
        [
            ("Hits", "Ann", 1),
            ("Hits", "Bob", 1),
            ("Hits", "Gina", 1),
            ("Hits", "X", 1),
            ("Hits", "Y", 1),
        ]);
    }

    private async Task<TrackDto> TrackAsync(string title)
    {
        PageDto<TrackDto> tracks = await _admin.GetJsonAsync<PageDto<TrackDto>>("/api/v1/tracks?limit=100");
        return tracks.Items.Single(track => track.Title == title);
    }

    private async Task<byte[]> CoverAsync(string url)
    {
        HttpResponseMessage response = await _admin.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task CoverShouldBeMissingAsync(string url)
    {
        await (await _admin.GetAsync(url)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "cover.not_found");
    }

    private Task AudioAsync(string path, TestTags tags)
    {
        return TestMediaGenerator.CreateAudioAsync(_media.Combine(path), AudioFormat.Mp3, tags);
    }
}
