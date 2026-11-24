using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using WaveMP.Core.ArtistPictures.Extensions;
using WaveMP.Core.ArtistPictures.Stores;
using WaveMP.Core.Caching.Extensions;
using WaveMP.Data.Repositories;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Core.ArtistPictures.Tests.Stores;

/// <summary>
/// Stores synthetic pictures with the real ffmpeg.
/// </summary>
[TestFixture]
[Category("Media")]
public class ArtistPictureStoreTests
{
    private TemporaryDirectory _data = null!;
    private TemporaryDirectory _pictures = null!;
    private ServiceProvider _provider = null!;
    private IArtistPictureStore _store = null!;

    private string RootPath => Path.Combine(_data.Path, "artist-pictures");

    [SetUp]
    public async Task SetUp()
    {
        _data = new TemporaryDirectory();
        _pictures = new TemporaryDirectory();
        _provider = CreateServices(_data.Path, FfmpegTool.FfmpegPath);
        _store = _provider.GetRequiredService<IArtistPictureStore>();
        await _store.InitializeAsync();
    }

    [TearDown]
    public async Task TearDown()
    {
        await _provider.DisposeAsync();
        _pictures.Dispose();
        _data.Dispose();
    }

    [Test]
    public async Task SaveAsync_WidePng_StoresSquarePicturesNamedAfterTheIdOnly()
    {
        var pictureId = Guid.NewGuid();
        await using Stream picture = await CreatePictureAsync("artist.png", 500, 320);

        ArtistPictureSaveResult result = await _store.SaveAsync(pictureId, picture);

        result.Should().Be(new ArtistPictureSaveResult(ArtistPictureSaveStatus.Saved, 320, 320));
        Directory.GetDirectories(RootPath).Select(Path.GetFileName)
            .Should().BeEquivalentTo(".tmp", pictureId.ToString("N"));
        Directory.GetFiles(Path.Combine(RootPath, pictureId.ToString("N"))).Select(Path.GetFileName)
            .Should().BeEquivalentTo("picture-96.webp", "picture-300.webp", "picture-1000.webp");
        (await SizeOfAsync(pictureId, 96)).Should().Be((96, 96));
        (await SizeOfAsync(pictureId, 300)).Should().Be((300, 300));
        (await SizeOfAsync(pictureId, 1000)).Should().Be((320, 320));
        Directory.GetFiles(Path.Combine(RootPath, ".tmp")).Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_WithCrop_StoresTheChosenSectionAndMeasuresIt()
    {
        var pictureId = Guid.NewGuid();
        await using Stream picture = await CreatePictureAsync("online.jpg", 600, 400);

        ArtistPictureSaveResult result = await _store.SaveAsync(
            pictureId,
            picture,
            new Caching.Images.CoverCrop(0.5, 0.25, 0.5));

        result.Should().Be(new ArtistPictureSaveResult(ArtistPictureSaveStatus.Saved, 200, 200));
        (await SizeOfAsync(pictureId, 1000)).Should().Be((200, 200));
    }

    [Test]
    public async Task SaveAsync_LargeJpeg_ReportsTheLargestStoredSize()
    {
        await using Stream picture = await CreatePictureAsync("photo.jpg", 1600, 1200);

        ArtistPictureSaveResult result = await _store.SaveAsync(Guid.NewGuid(), picture);

        result.Should().Be(new ArtistPictureSaveResult(ArtistPictureSaveStatus.Saved, 1000, 1000));
    }

    [Test]
    public async Task SaveAsync_Gif_ReturnsUnsupportedFormat()
    {
        await using Stream picture = await CreatePictureAsync("artist.gif", 100, 100);

        ArtistPictureSaveResult result = await _store.SaveAsync(Guid.NewGuid(), picture);

        result.Status.Should().Be(ArtistPictureSaveStatus.UnsupportedFormat);
    }

    [Test]
    public async Task SaveAsync_Markup_ReturnsUnsupportedFormat()
    {
        await using MemoryStream svg = new("<svg xmlns=\"http://www.w3.org/2000/svg\"><script/></svg>"u8.ToArray());

        ArtistPictureSaveResult result = await _store.SaveAsync(Guid.NewGuid(), svg);

        result.Status.Should().Be(ArtistPictureSaveStatus.UnsupportedFormat);
        _store.ListPictureIds().Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_DamagedJpeg_ReturnsUnreadableAndStoresNothing()
    {
        var pictureId = Guid.NewGuid();
        byte[] damaged = [0xFF, 0xD8, 0xFF, 0xE0, .. new byte[2048]];
        await using MemoryStream picture = new(damaged);

        ArtistPictureSaveResult result = await _store.SaveAsync(pictureId, picture);

        result.Status.Should().Be(ArtistPictureSaveStatus.Unreadable);
        _store.Open(pictureId, 96).Should().BeNull();
        _store.ListPictureIds().Should().BeEmpty();
        Directory.GetFiles(Path.Combine(RootPath, ".tmp")).Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_WithoutFfmpeg_ReturnsUnavailable()
    {
        await using ServiceProvider provider = CreateServices(_data.Path, _data.Combine("missing-ffmpeg"));
        IArtistPictureStore store = provider.GetRequiredService<IArtistPictureStore>();
        await store.InitializeAsync();
        await using Stream picture = await CreatePictureAsync("artist.png", 100, 100);

        ArtistPictureSaveResult result = await store.SaveAsync(Guid.NewGuid(), picture);

        result.Status.Should().Be(ArtistPictureSaveStatus.Unavailable);
    }

    [Test]
    public async Task Open_RequestedSize_RoundsItUp()
    {
        var pictureId = Guid.NewGuid();
        await using (Stream picture = await CreatePictureAsync("artist.png", 400, 400))
        {
            await _store.SaveAsync(pictureId, picture);
        }

        await using Stream? opened = _store.Open(pictureId, 120);

        opened.Should().NotBeNull();
        using MemoryStream copy = new();
        await opened!.CopyToAsync(copy);
        (await ImageProbe.GetSizeAsync(copy.ToArray())).Should().Be((300, 300));
    }

    [Test]
    public async Task Delete_Always_RemovesTheFolderOfThePicture()
    {
        var pictureId = Guid.NewGuid();
        await using (Stream picture = await CreatePictureAsync("artist.png", 100, 100))
        {
            await _store.SaveAsync(pictureId, picture);
        }

        _store.ListPictureIds().Should().Equal(pictureId);
        _store.Delete(pictureId);

        _store.ListPictureIds().Should().BeEmpty();
        _store.Open(pictureId, 96).Should().BeNull();
        Action again = () => _store.Delete(pictureId);
        again.Should().NotThrow();
    }

    [Test]
    public async Task InitializeAsync_Always_RemovesUnfinishedFilesAndIgnoresForeignFolders()
    {
        string unfinished = Path.Combine(RootPath, ".tmp", "left-over.webp");
        await File.WriteAllTextAsync(unfinished, "partial");
        Directory.CreateDirectory(Path.Combine(RootPath, "not-a-picture"));

        await _store.InitializeAsync();

        File.Exists(unfinished).Should().BeFalse();
        _store.IsAvailable.Should().BeTrue();
        _store.ListPictureIds().Should().BeEmpty();
    }

    private async Task<Stream> CreatePictureAsync(string name, int width, int height)
    {
        string path = _pictures.Combine(name);
        await TestMediaGenerator.CreateImageAsync(path, width, height);
        return new MemoryStream(await File.ReadAllBytesAsync(path));
    }

    private async Task<(int Width, int Height)> SizeOfAsync(Guid pictureId, int size)
    {
        return await ImageProbe.GetSizeAsync(
            Path.Combine(RootPath, pictureId.ToString("N"), $"picture-{size}.webp"));
    }

    private static ServiceProvider CreateServices(string dataPath, string ffmpegPath)
    {
        ServiceCollection services = new();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DATA_PATH"] = dataPath,
                ["Caching:FfmpegPath"] = ffmpegPath,
            })
            .Build());
        services.AddScoped(_ => Substitute.For<IArtistPictureRepository>());
        services.AddWaveMpCaching();
        services.AddWaveMpArtistPictures();
        return services.BuildServiceProvider();
    }
}
