using AwesomeAssertions;
using TagLib.Id3v2;
using WaveMP.Core.Metadata.Tags;
using WaveMP.Testing.Files;
using WaveMP.Testing.Media;

namespace WaveMP.Core.Metadata.Tests.Tags;

/// <summary>
/// Reads lyrics from synthetic media files created with ffmpeg, whose lyrics are written with TagLib#.
/// </summary>
[TestFixture]
[Category("Media")]
public class LyricsTagReaderTests
{
    private TemporaryDirectory _directory = null!;
    private TagLibLyricsTagReader _reader = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = new TemporaryDirectory();
        _reader = new TagLibLyricsTagReader();
    }

    [TearDown]
    public void TearDown()
    {
        _directory.Dispose();
    }

    [Test]
    public async Task Read_Mp3WithSyltAndUslt_ReadsBoth()
    {
        string path = await CreateAsync(AudioFormat.Mp3, "mp3");
        using (var file = TagLib.File.Create(path))
        {
            var tag = (TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, true);
            SynchronisedLyricsFrame frame = new("WaveMP", "eng", SynchedTextType.Lyrics)
            {
                Format = TimestampFormat.AbsoluteMilliseconds,
                Text = [new SynchedText(1000, "First"), new SynchedText(2500, "Second")],
            };
            tag.AddFrame(frame);
            tag.Lyrics = "First\nSecond";
            file.Save();
        }

        TagLyrics? lyrics = _reader.Read(path);

        lyrics.Should().NotBeNull();
        lyrics!.Synchronized.Should().Equal(
            new TimedLyricsText(TimeSpan.FromMilliseconds(1000), "First"),
            new TimedLyricsText(TimeSpan.FromMilliseconds(2500), "Second"));
        lyrics.Unsynchronized.Should().Be("First\nSecond");
    }

    [Test]
    public async Task Read_SyltWithMpegFrameTimestamps_IsSkipped()
    {
        string path = await CreateAsync(AudioFormat.Mp3, "mp3");
        using (var file = TagLib.File.Create(path))
        {
            var tag = (TagLib.Id3v2.Tag)file.GetTag(TagLib.TagTypes.Id3v2, true);
            tag.AddFrame(new SynchronisedLyricsFrame("WaveMP", "eng", SynchedTextType.Lyrics)
            {
                Format = TimestampFormat.AbsoluteMpegFrames,
                Text = [new SynchedText(10, "Frames")],
            });
            file.Save();
        }

        _reader.Read(path).Should().BeNull();
    }

    [TestCase(AudioFormat.Flac, "flac")]
    [TestCase(AudioFormat.Opus, "opus")]
    [TestCase(AudioFormat.M4a, "m4a")]
    public async Task Read_LyricsOfOtherFormats_AreUnsynchronized(AudioFormat format, string extension)
    {
        string path = await CreateAsync(format, extension);
        using (var file = TagLib.File.Create(path))
        {
            file.Tag.Lyrics = "Plain lyrics";
            file.Save();
        }

        TagLyrics? lyrics = _reader.Read(path);

        lyrics.Should().BeEquivalentTo(new TagLyrics([], "Plain lyrics"));
    }

    [Test]
    public async Task Read_VorbisUnsyncedLyricsComment_IsRead()
    {
        string path = await CreateAsync(AudioFormat.Flac, "flac");
        using (var file = TagLib.File.Create(path))
        {
            var comment = (TagLib.Ogg.XiphComment)file.GetTag(TagLib.TagTypes.Xiph, true);
            comment.SetField("UNSYNCEDLYRICS", "[00:01.00]From comment");
            file.Save();
        }

        _reader.Read(path)!.Unsynchronized.Should().Be("[00:01.00]From comment");
    }

    [Test]
    public async Task Read_FileWithoutLyrics_ReturnsNull()
    {
        string path = await CreateAsync(AudioFormat.Mp3, "mp3");

        _reader.Read(path).Should().BeNull();
    }

    [Test]
    public void Read_CorruptFile_ReturnsNull()
    {
        string path = _directory.Combine("broken.mp3");
        TestMediaGenerator.CreateCorruptFile(path);

        _reader.Read(path).Should().BeNull();
    }

    private async Task<string> CreateAsync(AudioFormat format, string extension)
    {
        string path = _directory.Combine($"song.{extension}");
        await TestMediaGenerator.CreateAudioAsync(path, format, new TestTags { Title = "Song" });
        return path;
    }
}
