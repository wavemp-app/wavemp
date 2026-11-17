using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WaveMP.Core.Caching.Extensions;
using WaveMP.Core.Users.Avatars;
using WaveMP.Core.Users.Extensions;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Core.Users.Tests.Avatars;

/// <summary>
/// Stores synthetic pictures with the real ffmpeg.
/// </summary>
[TestFixture]
[Category("Media")]
public class AvatarStoreTests
{
    private TemporaryDirectory _data = null!;
    private TemporaryDirectory _pictures = null!;
    private ServiceProvider _provider = null!;
    private IAvatarStore _store = null!;

    private string RootPath => Path.Combine(_data.Path, "avatars");

    [SetUp]
    public async Task SetUp()
    {
        _data = new TemporaryDirectory();
        _pictures = new TemporaryDirectory();
        _provider = CreateServices(_data.Path, FfmpegTool.FfmpegPath);
        _store = _provider.GetRequiredService<IAvatarStore>();
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
    public async Task SaveAsync_TallPng_StoresSquarePicturesInEverySizeThatAreNeverEnlarged()
    {
        var userId = Guid.NewGuid();
        await using Stream picture = await CreatePictureAsync("avatar.png", 360, 640);

        AvatarSaveStatus status = await _store.SaveAsync(userId, picture);

        status.Should().Be(AvatarSaveStatus.Saved);
        (await SizeOfAsync(userId, 96)).Should().Be((96, 96));
        (await SizeOfAsync(userId, 300)).Should().Be((300, 300));
        (await SizeOfAsync(userId, 1000)).Should().Be((360, 360));
        Directory.GetFiles(Path.Combine(RootPath, ".tmp")).Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_JpegAfterPng_ReplacesTheEarlierAvatar()
    {
        var userId = Guid.NewGuid();
        await using (Stream first = await CreatePictureAsync("first.png", 200, 200))
        {
            (await _store.SaveAsync(userId, first)).Should().Be(AvatarSaveStatus.Saved);
        }

        await using Stream second = await CreatePictureAsync("second.jpg", 640, 480);
        AvatarSaveStatus status = await _store.SaveAsync(userId, second);

        status.Should().Be(AvatarSaveStatus.Saved);
        (await SizeOfAsync(userId, 1000)).Should().Be((480, 480));
    }

    [Test]
    public async Task SaveAsync_WebP_IsAccepted()
    {
        var userId = Guid.NewGuid();
        await using Stream picture = await CreatePictureAsync("avatar.webp", 120, 120);

        AvatarSaveStatus status = await _store.SaveAsync(userId, picture);

        status.Should().Be(AvatarSaveStatus.Saved);
        (await SizeOfAsync(userId, 96)).Should().Be((96, 96));
    }

    [Test]
    public async Task SaveAsync_Gif_ReturnsUnsupportedFormat()
    {
        await using Stream picture = await CreatePictureAsync("avatar.gif", 100, 100);

        AvatarSaveStatus status = await _store.SaveAsync(Guid.NewGuid(), picture);

        status.Should().Be(AvatarSaveStatus.UnsupportedFormat);
    }

    [Test]
    public async Task SaveAsync_Text_ReturnsUnsupportedFormat()
    {
        await using MemoryStream text = new("not a picture"u8.ToArray());

        AvatarSaveStatus status = await _store.SaveAsync(Guid.NewGuid(), text);

        status.Should().Be(AvatarSaveStatus.UnsupportedFormat);
    }

    [Test]
    public async Task SaveAsync_DamagedPng_ReturnsUnreadableAndStoresNothing()
    {
        var userId = Guid.NewGuid();
        byte[] damaged = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, .. new byte[2048]];
        await using MemoryStream picture = new(damaged);

        AvatarSaveStatus status = await _store.SaveAsync(userId, picture);

        status.Should().Be(AvatarSaveStatus.Unreadable);
        _store.Open(userId, 96).Should().BeNull();
        Directory.GetFiles(Path.Combine(RootPath, ".tmp")).Should().BeEmpty();
    }

    [Test]
    public async Task SaveAsync_WithoutFfmpeg_ReturnsUnavailable()
    {
        await using ServiceProvider provider = CreateServices(_data.Path, _data.Combine("missing-ffmpeg"));
        IAvatarStore store = provider.GetRequiredService<IAvatarStore>();
        await store.InitializeAsync();
        await using Stream picture = await CreatePictureAsync("avatar.png", 100, 100);

        AvatarSaveStatus status = await store.SaveAsync(Guid.NewGuid(), picture);

        status.Should().Be(AvatarSaveStatus.Unavailable);
    }

    [Test]
    public async Task Open_RequestedSize_RoundsItUp()
    {
        var userId = Guid.NewGuid();
        await using (Stream picture = await CreatePictureAsync("avatar.png", 400, 400))
        {
            await _store.SaveAsync(userId, picture);
        }

        await using Stream? opened = _store.Open(userId, 40);

        opened.Should().NotBeNull();
        using MemoryStream copy = new();
        await opened!.CopyToAsync(copy);
        (await ImageProbe.GetSizeAsync(copy.ToArray())).Should().Be((96, 96));
    }

    [Test]
    public async Task Delete_Always_RemovesTheFolderOfTheProfile()
    {
        var userId = Guid.NewGuid();
        await using (Stream picture = await CreatePictureAsync("avatar.png", 100, 100))
        {
            await _store.SaveAsync(userId, picture);
        }

        _store.ListUserIds().Should().Contain(userId);
        _store.Delete(userId);

        _store.ListUserIds().Should().NotContain(userId);
        _store.Open(userId, 96).Should().BeNull();
        Action again = () => _store.Delete(userId);
        again.Should().NotThrow();
    }

    [Test]
    public async Task InitializeAsync_Always_RemovesUnfinishedFiles()
    {
        string unfinished = Path.Combine(RootPath, ".tmp", "left-over.webp");
        await File.WriteAllTextAsync(unfinished, "partial");

        await _store.InitializeAsync();

        File.Exists(unfinished).Should().BeFalse();
        _store.IsAvailable.Should().BeTrue();
        _store.ListUserIds().Should().BeEmpty();
    }

    private async Task<Stream> CreatePictureAsync(string name, int width, int height)
    {
        string path = _pictures.Combine(name);
        await TestMediaGenerator.CreateImageAsync(path, width, height);
        return new MemoryStream(await File.ReadAllBytesAsync(path));
    }

    private async Task<(int Width, int Height)> SizeOfAsync(Guid userId, int size)
    {
        return await ImageProbe.GetSizeAsync(Path.Combine(RootPath, userId.ToString("D"), $"avatar-{size}.webp"));
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
        services.AddWaveMpCaching();
        services.AddWaveMpUsers();
        return services.BuildServiceProvider();
    }
}
