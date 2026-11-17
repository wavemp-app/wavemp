using AwesomeAssertions;
using WaveMP.Core.Lyrics.Documents;
using WaveMP.Service.Dtos;
using WaveMP.Service.Mappings;

namespace WaveMP.Service.Tests.Mappings;

[TestFixture]
public class LyricsMappingExtensionsTests
{
    [Test]
    public void ToDto_WordSynchronizedLyrics_GivesTheTimesInMilliseconds()
    {
        TrackLyrics lyrics = new(
            new LyricsDocument(
                LyricsType.WordSynchronized,
                [
                    new LyricsLine(
                        TimeSpan.FromMilliseconds(1000),
                        TimeSpan.FromMilliseconds(2500),
                        "Hello world",
                        [
                            new LyricsWord(TimeSpan.FromMilliseconds(1000), TimeSpan.FromMilliseconds(1400), "Hello "),
                            new LyricsWord(TimeSpan.FromMilliseconds(1400), null, "world"),
                        ]),
                ]),
            LyricsSource.File);

        LyricsDto dto = lyrics.ToDto();

        dto.Should().BeEquivalentTo(new LyricsDto
        {
            Type = LyricsTypeDto.WordSynchronized,
            Source = LyricsSourceDto.File,
            Lines =
            [
                new LyricsLineDto
                {
                    StartMilliseconds = 1000,
                    EndMilliseconds = 2500,
                    Text = "Hello world",
                    Words =
                    [
                        new LyricsWordDto { StartMilliseconds = 1000, EndMilliseconds = 1400, Text = "Hello " },
                        new LyricsWordDto { StartMilliseconds = 1400, EndMilliseconds = null, Text = "world" },
                    ],
                },
            ],
        });
    }

    [Test]
    public void ToDto_StaticLine_HasNoTimes()
    {
        LyricsLineDto dto = new LyricsLine(null, null, "Line", []).ToDto();

        dto.StartMilliseconds.Should().BeNull();
        dto.EndMilliseconds.Should().BeNull();
        dto.Words.Should().BeEmpty();
    }

    [Test]
    public void ToDto_EveryTypeAndSource_HasItsOwnValue()
    {
        Enum.GetValues<LyricsType>().Select(type => type.ToDto().ToString())
            .Should().Equal(Enum.GetValues<LyricsType>().Select(type => type.ToString()));
        Enum.GetValues<LyricsSource>().Select(source => source.ToDto().ToString())
            .Should().Equal(Enum.GetValues<LyricsSource>().Select(source => source.ToString()));
    }
}
