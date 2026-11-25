using AwesomeAssertions;
using WaveMP.Core.Lyrics.Documents;
using WaveMP.Core.Lyrics.Parsers;

namespace WaveMP.Core.Lyrics.Tests.Parsers;

[TestFixture]
public class LrcParserTests
{
    [Test]
    public void Parse_LineTimestamps_ReturnsLineSynchronizedLinesWithTheirEnds()
    {
        LyricsDocument lyrics = LrcParser.Parse("""
            [00:01.00]First line
            [00:04.50]Second line
            [01:02.25]Third line
            """);

        lyrics.Type.Should().Be(LyricsType.LineSynchronized);
        lyrics.Lines.Should().BeEquivalentTo(
        [
            new LyricsLine(Ms(1000), Ms(4500), "First line", []),
            new LyricsLine(Ms(4500), Ms(62250), "Second line", []),
            new LyricsLine(Ms(62250), null, "Third line", []),
        ]);
    }

    [TestCase("[00:12]Line", 12000)]
    [TestCase("[00:12.3]Line", 12300)]
    [TestCase("[00:12.34]Line", 12340)]
    [TestCase("[00:12.345]Line", 12345)]
    [TestCase("[00:12:34]Line", 12340)]
    [TestCase("[100:00.00]Line", 6000000)]
    [TestCase("  [00:12.34]  Line  ", 12340)]
    public void Parse_TimestampFormats_ReadTheTime(string text, int milliseconds)
    {
        LyricsDocument lyrics = LrcParser.Parse(text);

        lyrics.Lines.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new LyricsLine(Ms(milliseconds), null, "Line", []));
    }

    [Test]
    public void Parse_LineWithSeveralTimestamps_IsSungAtEveryTime()
    {
        LyricsDocument lyrics = LrcParser.Parse("""
            [00:01.00][00:10.00]Chorus
            [00:05.00]Verse
            """);

        lyrics.Lines.Select(line => (line.Start, line.Text)).Should().Equal(
            (Ms(1000), "Chorus"),
            (Ms(5000), "Verse"),
            (Ms(10000), "Chorus"));
    }

    [Test]
    public void Parse_UnsortedLines_SortsThemByTimeAndKeepsTheOrderOfEqualTimes()
    {
        LyricsDocument lyrics = LrcParser.Parse("""
            [00:09.00]Last
            [00:01.00]First
            [00:05.00]Middle A
            [00:05.00]Middle B
            """);

        lyrics.Lines.Select(line => line.Text).Should().Equal("First", "Middle A", "Middle B", "Last");
    }

    [Test]
    public void Parse_IdTags_AreSkipped()
    {
        LyricsDocument lyrics = LrcParser.Parse("""
            [ar:Artist]
            [ti:Title]
            [al:Album]
            [by:Someone]
            [length: 03:20]
            [#:A comment]
            [00:01.00]Only line
            """);

        lyrics.Lines.Should().ContainSingle().Which.Text.Should().Be("Only line");
    }

    [TestCase("+500", 500)]
    [TestCase("-500", 1500)]
    [TestCase("+2000", 0)]
    public void Parse_Offset_ShiftsAllTimes(string offset, int expected)
    {
        LyricsDocument lyrics = LrcParser.Parse($"[offset:{offset}]\n[00:01.00]Line");

        lyrics.Lines.Should().ContainSingle().Which.Start.Should().Be(Ms(expected));
    }

    [Test]
    public void Parse_EmptyTimedLine_KeepsAPartWithoutSinging()
    {
        LyricsDocument lyrics = LrcParser.Parse("[00:01.00]Line\n[00:03.00]\n[00:05.00]Next");

        lyrics.Lines.Select(line => line.Text).Should().Equal("Line", string.Empty, "Next");
        lyrics.IsEmpty.Should().BeFalse();
    }

    [Test]
    public void Parse_WindowsLineBreaksAndByteOrderMark_AreIgnored()
    {
        LyricsDocument lyrics = LrcParser.Parse("﻿[00:01.00]First\r\n[00:02.00]Second\r\n");

        lyrics.Lines.Select(line => line.Text).Should().Equal("First", "Second");
    }

    [Test]
    public void Parse_InvalidSeconds_SkipsTheTimestamp()
    {
        LyricsDocument lyrics = LrcParser.Parse("[00:75.00]Wrong\n[00:01.00]Right");

        lyrics.Lines.Should().ContainSingle().Which.Text.Should().Be("Right");
    }

    [Test]
    public void Parse_EnhancedLrc_ReturnsWordSynchronizedLines()
    {
        LyricsDocument lyrics = LrcParser.Parse("""
            [00:01.00]<00:01.00>Hello <00:01.50>wide <00:02.00>world<00:02.80>
            [00:03.00]<00:03.00>Good<00:03.40>bye
            """);

        lyrics.Type.Should().Be(LyricsType.WordSynchronized);
        lyrics.Lines[0].Should().BeEquivalentTo(new LyricsLine(
            Ms(1000),
            Ms(3000),
            "Hello wide world",
            [
                new LyricsWord(Ms(1000), Ms(1500), "Hello "),
                new LyricsWord(Ms(1500), Ms(2000), "wide "),
                new LyricsWord(Ms(2000), Ms(2800), "world"),
            ]));
        lyrics.Lines[1].Should().BeEquivalentTo(new LyricsLine(
            Ms(3000),
            null,
            "Goodbye",
            [new LyricsWord(Ms(3000), Ms(3400), "Good"), new LyricsWord(Ms(3400), null, "bye")]));
    }

    [Test]
    public void Parse_EnhancedLineWithTextBeforeTheFirstWordTimestamp_StartsTheFirstWordWithTheLine()
    {
        LyricsDocument lyrics = LrcParser.Parse("[00:01.00]Hello <00:01.60>world");

        lyrics.Lines.Should().ContainSingle().Which.Words.Should().BeEquivalentTo(
        [
            new LyricsWord(Ms(1000), Ms(1600), "Hello "),
            new LyricsWord(Ms(1600), null, "world"),
        ]);
    }

    [Test]
    public void Parse_EnhancedLrcWithOffset_ShiftsTheWordsAsWell()
    {
        LyricsDocument lyrics = LrcParser.Parse("[offset:+500]\n[00:01.00]<00:01.00>One <00:02.00>two");

        lyrics.Lines[0].Start.Should().Be(Ms(500));
        lyrics.Lines[0].Words.Select(word => word.Start).Should().Equal(Ms(500), Ms(1500));
    }

    [Test]
    public void Parse_LinesWithASingleWordTimestamp_AreLineSynchronized()
    {
        LyricsDocument lyrics = LrcParser.Parse("[00:01.00]<00:01.00>First line\n[00:02.00]<00:02.00>Second line");

        lyrics.Type.Should().Be(LyricsType.LineSynchronized);
        lyrics.Lines.Should().AllSatisfy(line => line.Words.Should().BeEmpty());
        lyrics.Lines.Select(line => line.Text).Should().Equal("First line", "Second line");
    }

    [Test]
    public void Parse_MixedLines_KeepsLinesWithoutWordTimestampsWithoutWords()
    {
        LyricsDocument lyrics = LrcParser.Parse("[00:01.00]<00:01.00>One <00:01.50>two\n[00:03.00]Plain line");

        lyrics.Type.Should().Be(LyricsType.WordSynchronized);
        lyrics.Lines[1].Words.Should().BeEmpty();
        lyrics.Lines[1].Text.Should().Be("Plain line");
    }

    [Test]
    public void Parse_TextWithoutTimestamps_ReturnsStaticLinesWithSingleEmptyLinesBetweenParagraphs()
    {
        LyricsDocument lyrics = LrcParser.Parse("\n\n  First verse  \nsecond line\n\n\n\nChorus\n\n");

        lyrics.Type.Should().Be(LyricsType.Static);
        lyrics.Lines.Should().BeEquivalentTo(
        [
            new LyricsLine(null, null, "First verse", []),
            new LyricsLine(null, null, "second line", []),
            new LyricsLine(null, null, string.Empty, []),
            new LyricsLine(null, null, "Chorus", []),
        ]);
    }

    [TestCase("")]
    [TestCase("   \n \n")]
    [TestCase("[ar:Artist]\n[ti:Title]")]
    public void Parse_NoLyrics_IsEmpty(string text)
    {
        LrcParser.Parse(text).IsEmpty.Should().BeTrue();
    }

    [Test]
    public void Parse_ControlCharacters_AreRemoved()
    {
        LyricsDocument lyrics = LrcParser.Parse("[00:01.00]Bad\u0001 line\twith tab");

        lyrics.Lines.Should().ContainSingle().Which.Text.Should().Be("Bad line with tab");
    }

    [Test]
    public void Parse_HugeFile_KeepsAtMostTheLimitOfLines()
    {
        string text = string.Join(
            '\n',
            Enumerable.Range(0, 6000).Select(index => $"[{index / 60:00}:{index % 60:00}.00]L"));

        LrcParser.Parse(text).Lines.Should().HaveCount(5000);
    }

    private static TimeSpan Ms(int milliseconds)
    {
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
