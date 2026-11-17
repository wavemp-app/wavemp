using AwesomeAssertions;
using WaveMP.Core.Lyrics.Documents;
using WaveMP.Core.Lyrics.Parsers;
using WaveMP.Core.Metadata.Tags;

namespace WaveMP.Core.Lyrics.Tests.Parsers;

[TestFixture]
public class TagLyricsParserTests
{
    [Test]
    public void Parse_SynchronizedEntriesWithoutLineBreaks_AreLines()
    {
        TagLyrics tags = new(
            [Entry(3000, "Second line"), Entry(1000, " First line ")],
            "Plain lyrics");

        LyricsDocument lyrics = TagLyricsParser.Parse(tags);

        lyrics.Type.Should().Be(LyricsType.LineSynchronized);
        lyrics.Lines.Should().BeEquivalentTo(
        [
            new LyricsLine(Ms(1000), Ms(3000), "First line", []),
            new LyricsLine(Ms(3000), null, "Second line", []),
        ]);
    }

    [Test]
    public void Parse_SynchronizedEntriesWithLineBreaksAtTheStart_AreWordsOfLines()
    {
        TagLyrics tags = new(
            [
                Entry(1000, "Hel"),
                Entry(1300, "lo "),
                Entry(1600, "world"),
                Entry(3000, "\nSecond "),
                Entry(3500, "line"),
            ],
            null);

        LyricsDocument lyrics = TagLyricsParser.Parse(tags);

        lyrics.Type.Should().Be(LyricsType.WordSynchronized);
        lyrics.Lines.Should().BeEquivalentTo(
        [
            new LyricsLine(
                Ms(1000),
                Ms(3000),
                "Hello world",
                [
                    new LyricsWord(Ms(1000), Ms(1300), "Hel"),
                    new LyricsWord(Ms(1300), Ms(1600), "lo "),
                    new LyricsWord(Ms(1600), Ms(3000), "world"),
                ]),
            new LyricsLine(
                Ms(3000),
                null,
                "Second line",
                [new LyricsWord(Ms(3000), Ms(3500), "Second "), new LyricsWord(Ms(3500), null, "line")]),
        ]);
    }

    [Test]
    public void Parse_SynchronizedEntriesWithLineBreaksAtTheEnd_StartANewLineAfterThem()
    {
        TagLyrics tags = new([Entry(1000, "One "), Entry(1500, "two\r\n"), Entry(2000, "Three")], null);

        LyricsDocument lyrics = TagLyricsParser.Parse(tags);

        lyrics.Type.Should().Be(LyricsType.WordSynchronized);
        lyrics.Lines.Select(line => line.Text).Should().Equal("One two", "Three");
        lyrics.Lines[1].Words.Should().ContainSingle().Which.Text.Should().Be("Three");
    }

    [Test]
    public void Parse_UnsynchronizedLyricsWithLrcTimestamps_AreSynchronized()
    {
        TagLyrics tags = new([], "[00:01.00]First\n[00:02.00]Second");

        LyricsDocument lyrics = TagLyricsParser.Parse(tags);

        lyrics.Type.Should().Be(LyricsType.LineSynchronized);
        lyrics.Lines.Select(line => line.Text).Should().Equal("First", "Second");
    }

    [Test]
    public void Parse_PlainUnsynchronizedLyrics_AreStatic()
    {
        LyricsDocument lyrics = TagLyricsParser.Parse(new TagLyrics([], "First\nSecond"));

        lyrics.Type.Should().Be(LyricsType.Static);
        lyrics.Lines.Select(line => line.Text).Should().Equal("First", "Second");
    }

    [Test]
    public void Parse_EmptySynchronizedLyrics_FallBackToTheUnsynchronizedOnes()
    {
        LyricsDocument lyrics = TagLyricsParser.Parse(new TagLyrics([Entry(1000, "  ")], "Plain"));

        lyrics.Type.Should().Be(LyricsType.Static);
        lyrics.Lines.Should().ContainSingle().Which.Text.Should().Be("Plain");
    }

    [Test]
    public void Parse_NothingUsable_IsEmpty()
    {
        TagLyricsParser.Parse(new TagLyrics([Entry(1000, "")], "  ")).IsEmpty.Should().BeTrue();
    }

    private static TimedLyricsText Entry(int milliseconds, string text)
    {
        return new TimedLyricsText(Ms(milliseconds), text);
    }

    private static TimeSpan Ms(int milliseconds)
    {
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
