using System.Text;
using AwesomeAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using WaveMP.Core.Lyrics.Files;
using WaveMP.Core.Lyrics.Options;
using WaveMP.Core.Metadata.Files;
using WaveMP.Data.Entities;
using WaveMP.Testing.Files;

namespace WaveMP.Core.Lyrics.Tests.Files;

[TestFixture]
public class LyricsFileStoreTests
{
    private TemporaryDirectory _media = null!;
    private LyricsFileStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _media = new TemporaryDirectory();
        IMediaFileLocator locator = Substitute.For<IMediaFileLocator>();
        locator.TryGetFullPath(MediaSource.DefaultId, Arg.Any<string>(), out Arg.Any<string>())
            .Returns(call =>
            {
                string relative = call.ArgAt<string>(1);
                if (relative.Contains("..", StringComparison.Ordinal))
                {
                    return false;
                }

                call[2] = _media.Combine(relative);
                return true;
            });
        _store = new LyricsFileStore(
            locator,
            Microsoft.Extensions.Options.Options.Create(new LyricsOptions { MaxFileBytes = 100 }),
            NullLogger<LyricsFileStore>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _media.Dispose();
    }

    [Test]
    public async Task ReadAsync_ExistingFile_ReturnsItsText()
    {
        await File.WriteAllTextAsync(_media.Combine("Song.lrc"), "[00:01.00]Hello", new UTF8Encoding(true));

        string? text = await _store.ReadAsync(MediaSource.DefaultId, "Song.lrc");

        text.Should().Be("[00:01.00]Hello");
    }

    [Test]
    public async Task ReadAsync_MissingTooLargeOrOutsideFile_ReturnsNull()
    {
        await File.WriteAllTextAsync(_media.Combine("Large.lrc"), new string('x', 101));

        (await _store.ReadAsync(MediaSource.DefaultId, "Missing.lrc")).Should().BeNull();
        (await _store.ReadAsync(MediaSource.DefaultId, "Large.lrc")).Should().BeNull();
        (await _store.ReadAsync(MediaSource.DefaultId, "../Other.lrc")).Should().BeNull();
    }

    [Test]
    public async Task CreateAsync_NewFile_WritesUtf8WithoutByteOrderMark()
    {
        LyricsFileWriteStatus status = await _store.CreateAsync(MediaSource.DefaultId, "Song.lrc", "[00:01.00]Grüße");

        status.Should().Be(LyricsFileWriteStatus.Created);
        byte[] bytes = await File.ReadAllBytesAsync(_media.Combine("Song.lrc"));
        bytes.Should().Equal(new UTF8Encoding(false).GetBytes("[00:01.00]Grüße"));
    }

    [Test]
    public async Task CreateAsync_ExistingFile_KeepsIt()
    {
        await File.WriteAllTextAsync(_media.Combine("Song.lrc"), "Own lyrics");

        LyricsFileWriteStatus status = await _store.CreateAsync(MediaSource.DefaultId, "Song.lrc", "Downloaded");

        status.Should().Be(LyricsFileWriteStatus.Exists);
        (await File.ReadAllTextAsync(_media.Combine("Song.lrc"))).Should().Be("Own lyrics");
    }

    [Test]
    public async Task CreateAsync_MissingFolderOrPathOutsideTheMediaFolder_IsNotWritable()
    {
        (await _store.CreateAsync(MediaSource.DefaultId, "Gone/Song.lrc", "Lyrics")).Should().Be(
            LyricsFileWriteStatus.NotWritable);
        (await _store.CreateAsync(MediaSource.DefaultId, "../Song.lrc", "Lyrics")).Should().Be(
            LyricsFileWriteStatus.NotWritable);
    }
}
