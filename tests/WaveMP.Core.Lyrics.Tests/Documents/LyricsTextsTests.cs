using AwesomeAssertions;
using WaveMP.Core.Lyrics.Documents;
using WaveMP.Core.Lyrics.Parsers;

namespace WaveMP.Core.Lyrics.Tests.Documents;

[TestFixture]
public class LyricsTextsTests
{
    [Test]
    public void ToPlain_SynchronizedLyricsWithBreaks_KeepsOneEmptyLinePerBreak()
    {
        LyricsDocument document = LrcParser.Parse(
            "[00:00.50]\n[00:01.00]One\n[00:02.00]\n[00:02.50]\n[00:03.00]Two\n[00:04.00]");

        LyricsTexts.ToPlain(document).Should().Be("One\n\nTwo");
    }

    [Test]
    public void ToSynchronized_WordSynchronizedLyrics_WritesOneTimestampPerLine()
    {
        LyricsDocument document = LrcParser.Parse(
            "[00:01.50]<00:01.50>Hello <00:02.00>world\n[01:02.345]\n[61:00.00]Late");

        LyricsTexts.ToSynchronized(document).Should().Be("[00:01.50]Hello world\n[01:02.35]\n[61:00.00]Late");
    }

    [Test]
    public void ToSynchronized_StaticLyrics_IsNull()
    {
        LyricsTexts.ToSynchronized(LrcParser.Parse("Plain")).Should().BeNull();
    }

    [Test]
    public void Clean_UploadedText_RemovesTheByteOrderMarkAndUnifiesTheLineBreaks()
    {
        LyricsTexts.Clean("\uFEFF\r\n[00:01.00]One\r\n[00:02.00]Two\r\n\r\n")
            .Should().Be("[00:01.00]One\n[00:02.00]Two");
    }
}
